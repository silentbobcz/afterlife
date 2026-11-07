using System.Diagnostics;
using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>From profile to game: preview, apply with the journal, recovery, REDmod deploy and launch.</summary>
public sealed class DeployService
{
    private readonly InstallContext _ctx;
    private AfterlifeStore Store => _ctx.Store;
    private LibraryPaths Library => _ctx.LibraryPaths;
    private IFileOps _fs => _ctx.Fs;
    private IGameEnvironment _env => _ctx.Env;
    private string GameRoot => _ctx.GameRoot;
    private ProfileState SelectedProfile => _ctx.SelectedProfile;

    internal DeployService(InstallContext ctx) => _ctx = ctx;

    /// <param name="rescan">false: reuse the last scan of the game folder when only profiles changed (see <see cref="ScanService.CachedScan"/>).</param>
    public PreviewResult Preview(string? profileId = null, PlanDecisions? decisions = null, bool rescan = true)
    {
        var profile = profileId is null ? SelectedProfile : Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        var scan = rescan ? _ctx.Scanner.Scan() : _ctx.Scanner.CachedScan();
        var snapshot = BuildSnapshot(profile, scan);
        return MakePreview(snapshot, scan, decisions);
    }

    internal PreviewResult MakePreview(DeploymentSnapshot snapshot, ScanResult scan, PlanDecisions? decisions)
    {
        var ledger = Store.GetLedger();
        var observer = new GameStateObserver(GameRoot, _fs);
        var plan = Planner.Create(snapshot, ledger, observer, Store.GetBaseline(ModlistFormat.RelativePath), decisions);
        return new PreviewResult(snapshot, plan, scan, EnvironmentIssues());
    }

    internal DeploymentSnapshot BuildSnapshot(ProfileState profile, ScanResult scan)
    {
        var packages = Store.GetPackages().ToDictionary(p => p.PackageId);
        var incumbents = Store.GetLedger().Values.Where(l => l.Kind == ArtifactKind.Payload)
            .ToDictionary(l => l.Path.ToUpperInvariant(), l => l.Owner, StringComparer.Ordinal);
        return SnapshotBuilder.Build(new SnapshotInputs(profile, packages, scan.ExternalCapabilities, incumbents, scan.UnmanagedArchiveNames),
            "snap-" + Guid.NewGuid().ToString("N")[..12]);
    }

    public List<Issue> EnvironmentIssues()
    {
        var list = new List<Issue>();
        if (_env.IsGameRunning()) list.Add(new Issue(IssueKind.GameRunning, Severity.Blocking, "Cyberpunk 2077 is running; close it before changing mods"));
        var unfinished = Store.GetUnfinishedOperation();
        if (unfinished is not null) list.Add(new Issue(IssueKind.RecoveryRequired, Severity.Blocking, $"an operation did not finish ({unfinished.Status}); run recovery first"));
        if (BuildChanged(out var fp)) list.Add(new Issue(IssueKind.BuildChanged, Severity.Warning, $"game updated to {fp?.Display ?? "unknown"}; review before modded launch"));
        return list;
    }

    public bool BuildChanged(out GameFingerprint? current)
    {
        current = _env.Fingerprint(GameRoot);
        var ackJson = Store.GetMeta("game.fingerprint.ack");
        if (current is null || ackJson is null) return false;
        return !Json.Deserialize<GameFingerprint>(ackJson).SameExecutable(current);
    }

    public void AcknowledgeBuild()
    {
        var fp = _env.Fingerprint(GameRoot);
        if (fp is not null) Store.SetMeta("game.fingerprint.ack", Json.Serialize(fp));
    }

    public ApplyResult Apply(string? profileId = null, PlanDecisions? decisions = null, IProgress<DeployProgress>? progress = null)
    {
        using var _ = InstallLock.Acquire(Library, GameRoot);
        progress?.Report(new DeployProgress(DeployPhase.Preparing, 0, 0));
        var preview = Preview(profileId, decisions);
        ActivityLog.Plan($"apply profile {preview.Snapshot.ProfileId} ({preview.Snapshot.IncludedPackages.Count} mod(s) included, {preview.Snapshot.ExcludedPackages.Count} held back)", preview.Plan);
        if (!preview.CanApply)
        {
            var reason = "blocked: " + string.Join("; ", preview.Plan.Blockers.Concat(preview.EnvironmentIssues.Where(i => i.Severity == Severity.Blocking)).Select(i => i.Message));
            ActivityLog.Write("apply", reason);
            return new ApplyResult(preview, null, reason);
        }
        if (preview.Plan.IsNoop && preview.Snapshot.ProfileId == Store.GetLastGeneration()?.ProfileId)
            return new ApplyResult(preview, null, "already deployed");
        var exec = Run(preview.Snapshot, preview.Plan, progress);
        ActivityLog.Write("apply", $"{exec.Status} after {exec.StepsApplied} step(s){(exec.Error is null ? "" : ": " + exec.Error)}");
        return new ApplyResult(preview, exec, exec.Status == OperationStatus.Verified ? "deployed" : exec.Error ?? exec.Status.ToString());
    }

    internal ExecutionResult Run(DeploymentSnapshot snapshot, Plan plan, IProgress<DeployProgress>? progress = null)
    {
        var opId = "op-" + Guid.NewGuid().ToString("N");
        var fpBefore = _env.Fingerprint(GameRoot);
        var steps = plan.Steps.Select(s => new StepRecord(opId, s,
            s.Kind is StepKind.Add or StepKind.Replace ? DeploymentExecutor.TempPathFor(GamePath.Parse(s.Path).ToOsPath(GameRoot), opId, s.Index) : null,
            StepState.Planned, null)).ToList();
        Store.CreateOperation(new OperationRecord(opId, DateTimeOffset.UtcNow, OperationStatus.Planned, snapshot.ProfileId, snapshot.ProfileRevision,
            Json.Serialize(snapshot), Store.GetLastGeneration()?.Id, fpBefore is null ? null : Json.Serialize(fpBefore), null), steps);
        var executor = new DeploymentExecutor(Store, Library, GameRoot, _fs, () =>
        {
            if (_env.IsGameRunning()) return "the game was started during deployment";
            var fp = _env.Fingerprint(GameRoot);
            if (fp != fpBefore) return "the game files changed during deployment";
            return null;
        });
        return executor.Execute(opId, progress);
    }

    public OperationRecord? PendingRecovery => Store.GetUnfinishedOperation();

    public (RecoveryOutcome Outcome, ApplyResult Followup) Recover(RecoveryChoice choice, IProgress<DeployProgress>? progress = null)
    {
        using var _ = InstallLock.Acquire(Library, GameRoot);
        var op = Store.GetUnfinishedOperation() ?? throw new InvalidOperationException("nothing to recover");
        var executor = new DeploymentExecutor(Store, Library, GameRoot, _fs);
        var outcome = executor.Recover(op.Id);

        DeploymentSnapshot target;
        if (choice == RecoveryChoice.Finish) target = Json.Deserialize<DeploymentSnapshot>(op.TargetSnapshotJson);
        else
        {
            var prev = op.PreviousGenerationId is null ? null : Store.GetGeneration(op.PreviousGenerationId);
            target = prev is null
                ? SnapshotBuilder.Build(new SnapshotInputs(ProfileState.CreateModsOff(), new Dictionary<string, PackageManifest>(), new HashSet<string>(),
                    new Dictionary<string, string>(), []), "snap-" + Guid.NewGuid().ToString("N")[..12])
                : Json.Deserialize<DeploymentSnapshot>(prev.SnapshotJson);
        }
        var scan = _ctx.Scanner.Scan();
        var preview = MakePreview(target with { Id = "snap-" + Guid.NewGuid().ToString("N")[..12] }, scan, null);
        if (!preview.CanApply)
            return (outcome, new ApplyResult(preview, null, "the record of what is in the game was repaired, but the game could not be brought back yet: " +
                string.Join("; ", preview.Plan.Blockers.Concat(preview.EnvironmentIssues.Where(i => i.Severity == Severity.Blocking)).Select(i => i.Message))));
        var exec = Run(preview.Snapshot, preview.Plan, progress);
        return (outcome, new ApplyResult(preview, exec, exec.Status == OperationStatus.Verified ? "recovered" : exec.Error ?? exec.Status.ToString()));
    }

    public const string RedModTool = "tools/redmod/bin/redMod.exe";

    public const string RedModLog = "tools/redmod/bin/REDmodLog.txt";

    private const string RedModSignatureKey = "redmod.signature";

    /// <summary>REDmods in the game (folders under mods/ written by AFTERLIFE), in the profile's load order: first wins.</summary>
    public IReadOnlyList<string> DeployedRedMods()
    {
        var ledger = Store.GetLedger().Values.Where(l => l.Path.StartsWith("mods/", StringComparison.OrdinalIgnoreCase)).ToList();
        if (ledger.Count == 0) return [];
        var byOwner = ledger.GroupBy(l => l.Owner).ToDictionary(g => g.Key, g => g.Select(l => l.Path.Split('/')[1]).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        var order = SelectedProfile.Entries.Select(e => e.PackageId).Concat(byOwner.Keys).Distinct().ToList();
        return order.Where(byOwner.ContainsKey).SelectMany(id => byOwner[id]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// REDmods only work after redMod.exe compiled them and the game starts with -modded. Runs the deploy when
    /// The REDmods or their files changed since the last successful run (like Vortex does on launch).
    /// </summary>
    public RedModResult DeployRedMods(bool force = false)
    {
        var mods = DeployedRedMods();
        if (mods.Count == 0) return new RedModResult(RedModOutcome.NoRedMods, mods, "no REDmods in the game");
        var tool = GamePath.Parse(RedModTool).ToOsPath(GameRoot);
        if (!_fs.FileExists(tool))
            return new RedModResult(RedModOutcome.ToolMissing, mods,
                "REDmod is not installed: add the free REDmod DLC (Steam: DLC page, GOG: Galaxy → Manage installation, Epic: add-ons). The game starts without the REDmods.");
        var ledger = Store.GetLedger().Values.Where(l => l.Path.StartsWith("mods/", StringComparison.OrdinalIgnoreCase)).OrderBy(l => l.Path, StringComparer.OrdinalIgnoreCase);
        var signature = ContentHash.Of(string.Join("\n", mods) + "|" + string.Join("\n", ledger.Select(l => l.Path + ":" + l.Hash)) + "|" + (_env.Fingerprint(GameRoot)?.FileVersion ?? ""));
        var cache = GamePath.Parse("r6/cache/modded").ToOsPath(GameRoot);
        if (!force && Store.GetMeta(RedModSignatureKey) == signature && Directory.Exists(cache))
            return new RedModResult(RedModOutcome.UpToDate, mods, "REDmods already deployed");
        Directory.CreateDirectory(Library.Staging);
        var list = Path.Combine(Library.Staging, "redmod-load-order.txt");
        File.WriteAllLines(list, mods);
        var code = _env.RunTool(tool, ["deploy", "-reportProgress", $"-root={Path.GetFullPath(GameRoot)}", $"-modlist={list}"],
            Path.GetDirectoryName(tool)!, TimeSpan.FromMinutes(15));
        _ctx.Scanner.InvalidateScan(); // redMod.exe writes r6/cache/modded
        if (code == 0)
        {
            Store.SetMeta(RedModSignatureKey, signature);
            return new RedModResult(RedModOutcome.Deployed, mods, $"REDmod deployed {mods.Count} mod(s)");
        }
        Store.SetMeta(RedModSignatureKey, "");
        var logPath = GamePath.Parse(RedModLog).ToOsPath(GameRoot);
        string tail = "";
        try { if (File.Exists(logPath)) tail = string.Join(" ", File.ReadLines(logPath).TakeLast(3)); } catch (IOException) { }
        return new RedModResult(RedModOutcome.Failed, mods,
            (code is null ? "REDmod did not finish in 15 minutes" : $"REDmod failed (exit code {code})") + (tail.Length == 0 ? "" : ": " + tail));
    }

    public Process LaunchGame(string arguments = "--launcher-skip", bool allowChangedBuild = false)
    {
        var issues = EnvironmentIssues();
        if (issues.Any(i => i.Severity == Severity.Blocking)) throw new InvalidOperationException(string.Join("; ", issues.Select(i => i.Message)));
        if (!allowChangedBuild && issues.Any(i => i.Kind == IssueKind.BuildChanged))
            throw new InvalidOperationException("the game was updated; review and acknowledge the build before a modded launch");
        var last = Store.GetLastGeneration();
        if (last is null || last.ProfileId != SelectedProfile.Id)
            throw new InvalidOperationException("the selected profile is not deployed yet");
        var exe = GamePath.Parse(WindowsGameEnvironment.ExeRelativePath).ToOsPath(GameRoot);
        _ctx.Scanner.InvalidateScan(); // the game and its mods write settings, logs and caches while it runs
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        foreach (var a in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)) psi.ArgumentList.Add(a);
        return Process.Start(psi) ?? throw new InvalidOperationException("could not start the game");
    }

    public StatusReport Status()
    {
        var changed = BuildChanged(out var fp);
        return new StatusReport(GameRoot, SelectedProfile, Store.GetLastGeneration(), Store.GetUnfinishedOperation(), fp, changed,
            Store.GetLedger().Count, Store.GetPackages().Count);
    }
}
