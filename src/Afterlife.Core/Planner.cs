namespace Afterlife.Core;

public enum ArtifactKind { Payload, Modlist }

/// <summary>What AFTERLIFE last proved it wrote to a game path (the ownership ledger).</summary>
public sealed record LedgerEntry(string Path, ArtifactKind Kind, string Owner, string Hash, long Size, long MTimeTicks);

public sealed record Observation(bool Exists, long Size, long MTimeTicks)
{
    public static Observation Absent { get; } = new(false, 0, 0);
    public bool FastMatches(LedgerEntry e) => Exists && Size == e.Size && MTimeTicks == e.MTimeTicks;
}

/// <summary>Port: read-only view of the game folder.</summary>
public interface IStateObserver
{
    Observation Observe(GamePath path);
    string FullHash(GamePath path);
}

public enum StepKind
{
    /// <summary>Create a file that must not exist yet (never overwrites).</summary>
    Add,
    /// <summary>Replace a file whose current content must equal ExpectedHash.</summary>
    Replace,
    /// <summary>Delete a file whose current content must equal ExpectedHash.</summary>
    Remove,
    /// <summary>Ledger-only: the file is already gone.</summary>
    Forget,
    /// <summary>Ledger-only: same content, new owner/metadata.</summary>
    Retag,
}

public enum BaselineAction { None, CaptureAbsent, CaptureCurrent, Clear }

public sealed record PlanStep(
    int Index,
    StepKind Kind,
    string Path,
    ArtifactKind Artifact,
    string? Owner,
    string? ExpectedHash,
    string? NewHash,
    long NewSize,
    string? SourceStoredPath,
    string? SourceContent,
    BaselineAction Baseline,
    string Reason);

public sealed record Plan(IReadOnlyList<PlanStep> Steps, IReadOnlyList<Issue> Blockers, IReadOnlyList<Issue> Warnings)
{
    public bool CanApply => Blockers.Count == 0;
    public bool IsNoop => Steps.Count == 0;
}

public sealed record BaselineInfo(bool Existed, string? Hash, string? Content);

public sealed record PlanDecisions(IReadOnlySet<string> DiscardChangesPathKeys)
{
    public static PlanDecisions None { get; } = new(new HashSet<string>());
}

/// <summary>
/// Diff between the frozen snapshot and the observed game folder. Pure: all IO goes through IStateObserver.
/// Rule: a path is changed only if the ledger proves ownership and its content is as expected,
/// or it does not exist (Add never overwrites).
/// </summary>
public static class Planner
{
    public static Plan Create(
        DeploymentSnapshot snapshot,
        IReadOnlyDictionary<string, LedgerEntry> ledger,
        IStateObserver observer,
        BaselineInfo? modlistBaseline,
        PlanDecisions? decisions = null)
    {
        decisions ??= PlanDecisions.None;
        var removes = new List<PlanStep>();
        var replaces = new List<PlanStep>();
        var adds = new List<PlanStep>();
        var retags = new List<PlanStep>();
        var modlistSteps = new List<PlanStep>();
        var blockers = snapshot.Issues.Where(i => i.Severity == Severity.Blocking && i.PackageId is null).ToList();
        var warnings = snapshot.Issues.Where(i => i.Severity != Severity.Blocking || i.PackageId is not null).ToList();
        var modlistKey = ModlistFormat.RelativePath.ToUpperInvariant();

        var desiredKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in snapshot.Files.OrderBy(f => f.Path.ToUpperInvariant(), StringComparer.Ordinal))
        {
            var gp = GamePath.Parse(f.Path);
            desiredKeys.Add(gp.Key);
            if (gp.Key == modlistKey)
            {
                blockers.Add(new Issue(IssueKind.ForeignFileAtTarget, Severity.Blocking, "packages may not ship modlist.txt", f.OwnerPackageId, f.Path));
                continue;
            }
            var obs = observer.Observe(gp);
            if (ledger.TryGetValue(gp.Key, out var le) && le.Kind == ArtifactKind.Payload)
            {
                if (!obs.Exists)
                {
                    adds.Add(Step(StepKind.Add, f.Path, ArtifactKind.Payload, f.OwnerPackageId, null, f.Hash, f.Size, f.StoredPath, null, "restore missing managed file"));
                    continue;
                }
                var current = obs.FastMatches(le) ? le.Hash : observer.FullHash(gp);
                if (current == le.Hash)
                {
                    if (le.Hash == f.Hash)
                    {
                        if (le.Owner != f.OwnerPackageId || !obs.FastMatches(le))
                            retags.Add(Step(StepKind.Retag, f.Path, ArtifactKind.Payload, f.OwnerPackageId, le.Hash, f.Hash, f.Size, null, null, "ownership/metadata update"));
                    }
                    else
                        replaces.Add(Step(StepKind.Replace, f.Path, ArtifactKind.Payload, f.OwnerPackageId, le.Hash, f.Hash, f.Size, f.StoredPath, null, "new content for managed path"));
                }
                else if (current == f.Hash)
                    retags.Add(Step(StepKind.Retag, f.Path, ArtifactKind.Payload, f.OwnerPackageId, current, f.Hash, f.Size, null, null, "content already as desired"));
                else if (decisions.DiscardChangesPathKeys.Contains(gp.Key))
                    replaces.Add(Step(StepKind.Replace, f.Path, ArtifactKind.Payload, f.OwnerPackageId, current, f.Hash, f.Size, f.StoredPath, null, "discard user changes"));
                else
                    blockers.Add(new Issue(IssueKind.ModifiedManagedFile, Severity.Blocking,
                        $"{f.Path} was changed outside AFTERLIFE; capture the changes as a local mod or discard them", le.Owner, f.Path));
            }
            else
            {
                if (!obs.Exists)
                    adds.Add(Step(StepKind.Add, f.Path, ArtifactKind.Payload, f.OwnerPackageId, null, f.Hash, f.Size, f.StoredPath, null, "new file"));
                else if (obs.Size == f.Size && observer.FullHash(gp) == f.Hash)
                    // P31: the very same file is already there (left from a manual install): adopt it instead of blocking.
                    retags.Add(Step(StepKind.Retag, f.Path, ArtifactKind.Payload, f.OwnerPackageId, f.Hash, f.Hash, f.Size, null, null, "identical file already in game; adopted"));
                else
                    blockers.Add(new Issue(IssueKind.ForeignFileAtTarget, Severity.Blocking,
                        $"{f.Path} is already in the game (not written by AFTERLIFE, different content)", f.OwnerPackageId, f.Path));
            }
        }

        foreach (var le in ledger.Values.Where(l => l.Kind == ArtifactKind.Payload).OrderBy(l => l.Path.ToUpperInvariant(), StringComparer.Ordinal))
        {
            var gp = GamePath.Parse(le.Path);
            if (desiredKeys.Contains(gp.Key)) continue;
            var obs = observer.Observe(gp);
            if (!obs.Exists)
            {
                removes.Add(Step(StepKind.Forget, le.Path, ArtifactKind.Payload, le.Owner, null, null, 0, null, null, "already absent"));
                continue;
            }
            var current = obs.FastMatches(le) ? le.Hash : observer.FullHash(gp);
            if (current == le.Hash)
                removes.Add(Step(StepKind.Remove, le.Path, ArtifactKind.Payload, le.Owner, le.Hash, null, 0, null, null, "no longer in profile"));
            else if (decisions.DiscardChangesPathKeys.Contains(gp.Key))
                removes.Add(Step(StepKind.Remove, le.Path, ArtifactKind.Payload, le.Owner, current, null, 0, null, null, "discard user changes"));
            else
                blockers.Add(new Issue(IssueKind.ModifiedManagedFile, Severity.Blocking,
                    $"{le.Path} was changed outside AFTERLIFE; capture the changes as a local mod or discard them", le.Owner, le.Path));
        }

        PlanModlist(snapshot, ledger, observer, modlistBaseline, decisions, modlistSteps, blockers);

        var steps = new List<PlanStep>();
        steps.AddRange(removes);
        steps.AddRange(replaces);
        steps.AddRange(adds);
        steps.AddRange(retags);
        steps.AddRange(modlistSteps);
        var indexed = steps.Select((s, i) => s with { Index = i }).ToList();
        return new Plan(indexed, blockers, warnings);
    }

    private static void PlanModlist(
        DeploymentSnapshot snapshot,
        IReadOnlyDictionary<string, LedgerEntry> ledger,
        IStateObserver observer,
        BaselineInfo? baseline,
        PlanDecisions decisions,
        List<PlanStep> steps,
        List<Issue> blockers)
    {
        var path = GamePath.Parse(ModlistFormat.RelativePath);
        ledger.TryGetValue(path.Key, out var le);
        if (le is not null && le.Kind != ArtifactKind.Modlist) le = null;
        var desired = snapshot.Modlist;
        var obs = observer.Observe(path);
        string? current = null;
        if (obs.Exists) current = le is not null && obs.FastMatches(le) ? le.Hash : observer.FullHash(path);

        if (desired is not null)
        {
            if (le is null)
            {
                if (!obs.Exists)
                    steps.Add(Step(StepKind.Add, path.Value, ArtifactKind.Modlist, "afterlife", null, desired.Hash, desired.Content.Length, null, desired.Content, BaselineAction.CaptureAbsent, "custom archive order"));
                else if (snapshot.ModlistAllowTakeover)
                    steps.Add(Step(StepKind.Replace, path.Value, ArtifactKind.Modlist, "afterlife", current, desired.Hash, desired.Content.Length, null, desired.Content, BaselineAction.CaptureCurrent, "take over existing modlist.txt"));
                else
                    blockers.Add(new Issue(IssueKind.ModlistForeignNeedsTakeover, Severity.Blocking,
                        "modlist.txt already exists; allow AFTERLIFE to take it over (the original is kept and restored later)", Path: path.Value));
                return;
            }
            if (!obs.Exists)
            {
                steps.Add(Step(StepKind.Add, path.Value, ArtifactKind.Modlist, "afterlife", null, desired.Hash, desired.Content.Length, null, desired.Content, BaselineAction.None, "recreate managed modlist.txt"));
                return;
            }
            if (current != le.Hash && !decisions.DiscardChangesPathKeys.Contains(path.Key))
            {
                blockers.Add(new Issue(IssueKind.ModlistChangedExternally, Severity.Blocking, "modlist.txt was changed outside AFTERLIFE", Path: path.Value));
                return;
            }
            if (current != desired.Hash)
                steps.Add(Step(StepKind.Replace, path.Value, ArtifactKind.Modlist, "afterlife", current, desired.Hash, desired.Content.Length, null, desired.Content, BaselineAction.None, "update archive order"));
            else if (!obs.FastMatches(le))
                steps.Add(Step(StepKind.Retag, path.Value, ArtifactKind.Modlist, "afterlife", current, desired.Hash, desired.Content.Length, null, null, BaselineAction.None, "metadata update"));
            return;
        }

        if (le is null) return; // not managing modlist and no desire to: leave any foreign file alone.

        // Leaving custom order: restore the original state (or its absence) only if the file is still ours.
        if (!obs.Exists)
        {
            if (baseline is { Existed: true, Content: not null })
                steps.Add(Step(StepKind.Add, path.Value, ArtifactKind.Modlist, "afterlife", null, baseline.Hash, baseline.Content.Length, null, baseline.Content, BaselineAction.Clear, "restore original modlist.txt"));
            else
                steps.Add(Step(StepKind.Forget, path.Value, ArtifactKind.Modlist, "afterlife", null, null, 0, null, null, BaselineAction.Clear, "already absent"));
            return;
        }
        if (current != le.Hash && !decisions.DiscardChangesPathKeys.Contains(path.Key))
        {
            blockers.Add(new Issue(IssueKind.ModlistChangedExternally, Severity.Blocking, "modlist.txt was changed outside AFTERLIFE; not restoring it blindly", Path: path.Value));
            return;
        }
        if (baseline is { Existed: true, Content: not null })
            steps.Add(Step(StepKind.Replace, path.Value, ArtifactKind.Modlist, "afterlife", current, baseline.Hash, baseline.Content.Length, null, baseline.Content, BaselineAction.Clear, "restore original modlist.txt"));
        else
            steps.Add(Step(StepKind.Remove, path.Value, ArtifactKind.Modlist, "afterlife", current, null, 0, null, null, BaselineAction.Clear, "restore original absence of modlist.txt"));
    }

    private static PlanStep Step(StepKind k, string path, ArtifactKind a, string? owner, string? expected, string? newHash, long size,
        string? source, string? content, string reason) =>
        new(0, k, path, a, owner, expected, newHash, size, source, content, BaselineAction.None, reason);

    private static PlanStep Step(StepKind k, string path, ArtifactKind a, string? owner, string? expected, string? newHash, long size,
        string? source, string? content, BaselineAction baseline, string reason) =>
        new(0, k, path, a, owner, expected, newHash, size, source, content, baseline, reason);
}

public enum StepState { Planned, Prepared, Applied, Failed }

public enum RecoveryVerdict { NotApplied, Applied, Unknown }

public static class RecoveryClassifier
{
    /// <summary>
    /// Decides what happened to a step that was durably marked Prepared (the intent record is the proof of
    /// ownership; the hash only tells which side of the change the disk is on).
    /// </summary>
    public static RecoveryVerdict Classify(PlanStep step, bool exists, string? hash) => step.Kind switch
    {
        StepKind.Add => !exists ? RecoveryVerdict.NotApplied : hash == step.NewHash ? RecoveryVerdict.Applied : RecoveryVerdict.Unknown,
        StepKind.Replace => exists && hash == step.ExpectedHash ? RecoveryVerdict.NotApplied
            : exists && hash == step.NewHash ? RecoveryVerdict.Applied : RecoveryVerdict.Unknown,
        StepKind.Remove => !exists ? RecoveryVerdict.Applied : hash == step.ExpectedHash ? RecoveryVerdict.NotApplied : RecoveryVerdict.Unknown,
        _ => RecoveryVerdict.NotApplied,
    };
}
