using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>Colour role of a status (AFTERLIFE-architektura-v2.1.md, "Barvy a priorita sdělení").</summary>
public enum Tone { Neutral, Ok, Info, Warn, Block, Brand, Plain, Category }

public enum ModState
{
    Off,
    Deployed,
    PendingAdd,
    PendingUpdate,
    PendingRemoval,
    MissingRequirement,
    Conflict,
    FileInTheWay,
    ChangedOutside,
    HeldBack,
    UserLayerInactive,
}

public sealed record ProfileSummary(string Id, string Name, bool IsSystem, int Wanted, bool IsSelected, bool IsDeployed);

public sealed record RequirementStatus(string Capability, string Name, string Status, Tone Tone, string? ProviderPackageId, bool CanOverride)
{
    /// <summary>P46: where it was found: "files", "scripts", "Nexus page" or a combination.</summary>
    public string Source { get; init; } = "files";
    /// <summary>P47: page of the required mod on Nexus, when known.</summary>
    public string? NexusUrl { get; init; }
}

public sealed record ConflictInfo(string Path, string? WinnerName, bool ThisModWins, IReadOnlyList<string> Providers);

public sealed record InstallMapLine(string Folder, string Summary);

public sealed record FrameworkStatus(string Name, string Detail, Tone Tone);

public enum CoreSource { InGame, Library, Manual, Missing }

public sealed record NexusRequirementStatus(string Name, string Url, string Status, Tone Tone, bool External, string? Notes)
{
    public string? ModId { get; init; }
    /// <summary>P49: Nexus has no "optional" flag; a note starting with Optional/Recommended marks it optional.</summary>
    public bool Optional => NexusInfo.IsOptionalNote(Notes);
}

/// <summary>P53: a mod in the library that is switched off but needed by a mod being switched on.</summary>
public sealed record InactiveRequirement(string PackageId, string Name, string NeededBy, string What);

/// <summary>One row of the core table in the header (P12).</summary>
public sealed record CoreModStatus(string Capability, string Name, string? Version, CoreSource Source, string? NexusUrl, bool Optional, string? PackageId)
{
    public string SourceLabel => Source switch
    {
        CoreSource.InGame => "AFTERLIFE",
        CoreSource.Library => "LIBRARY",
        CoreSource.Manual => "MANUAL",
        _ => Optional ? "OPTIONAL" : "MISSING",
    };
}

public sealed record ModOverview(
    PackageManifest Package,
    bool Wanted,
    bool InGame,
    bool Included,
    ModState State,
    string StateLabel,
    Tone Tone,
    bool HasOverride,
    string Explanation,
    IReadOnlyList<RequirementStatus> Requirements,
    IReadOnlyList<ConflictInfo> Conflicts,
    IReadOnlyList<InstallMapLine> InstallMap,
    IReadOnlyList<Issue> Issues)
{
    /// <summary>P41: Nexus mod id; confirmed when downloaded/linked or a core mod from the catalog, else guessed from the file name.</summary>
    public string? NexusId { get; init; }
    public bool NexusConfirmed { get; init; }
    /// <summary>P43: cached description of the Nexus page.</summary>
    public NexusModInfo? Nexus { get; init; }
    /// <summary>P40/P43: mods the Nexus page lists as required, with their status here.</summary>
    public IReadOnlyList<NexusRequirementStatus> RequirementLinks { get; init; } = [];
    public string? NexusUrl => NexusId is null ? null : $"https://www.nexusmods.com/cyberpunk2077/mods/{NexusId}";

    /// <summary>M4: a newer file on Nexus.</summary>
    public NexusUpdate? Update { get; init; }

    /// <summary>P51: collections this mod belongs to (names).</summary>
    public IReadOnlyList<string> Collections { get; init; } = [];
    public bool CollectionOptional { get; init; }
    /// <summary>P51: the collection fixes its place in the load order of this profile.</summary>
    public bool OrderLocked { get; init; }

    /// <summary>P49: required mods that are missing (files or Nexus page, not optional ones), for the warning in the row.</summary>
    public IReadOnlyList<string> Missing =>
        Requirements.Where(r => r.Tone == Tone.Block || r.Status is "not in library" or "in library · not enabled").Select(r => r.Name)
            .Concat(RequirementLinks.Where(r => !r.External && !r.Optional && r.Status == "not in library").Select(r => r.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Provides a framework capability: listed in the CORE chapter (P23).</summary>
    public bool IsCore => Package.Provides.Any(c => c.StartsWith("framework:", StringComparison.Ordinal));
}

public sealed record InstallOverview(
    StatusReport Status,
    IReadOnlyList<ProfileSummary> Profiles,
    IReadOnlyList<ModOverview> Mods,
    PreviewResult Preview,
    IReadOnlyList<FrameworkStatus> Frameworks,
    IReadOnlyList<(string Path, long Size)> Unmanaged,
    IReadOnlyList<string> ArchiveNames,
    bool CanLaunch,
    string LaunchHint,
    string ChangesSummary)
{
    public int PendingChanges => Preview.Plan.Steps.Count;
    public IReadOnlyList<DetectedMod> Detected => Preview.Scan.Detected;
    public IReadOnlyList<RemovedBatch> RemovedBatches { get; init; } = [];
    public IReadOnlyList<CoreModStatus> CoreMods { get; init; } = [];
    /// <summary>The selected profile uses its own load order (P25); otherwise the game loads archives alphabetically.</summary>
    public bool OrderIsCustom { get; init; }
    public bool IsDeployedProfile => Status.LastGeneration?.ProfileId == Status.Selected.Id;
}

/// <summary>
/// Read model for the main window: derives what each row, chip and detail tab should say from one preview.
/// Pure derivation over coordinator state; no writes besides the scan that Preview already does.
/// </summary>
public static class OverviewBuilder
{
    /// <summary>
    /// P53: mods that the given mods need (directly or through another needed mod) which are in the library but
    /// switched off in the selected profile. Requirements from files and the Nexus page (not the ones marked optional).
    /// </summary>
    public static IReadOnlyList<InactiveRequirement> InactiveRequirements(InstallOverview o, IEnumerable<string> packageIds)
    {
        var byId = o.Mods.ToDictionary(m => m.Package.PackageId, StringComparer.Ordinal);
        var turningOn = packageIds.ToHashSet(StringComparer.Ordinal);
        var result = new List<InactiveRequirement>();
        var seen = new HashSet<string>(turningOn, StringComparer.Ordinal);
        var queue = new Queue<string>(turningOn);
        while (queue.Count > 0)
        {
            if (!byId.TryGetValue(queue.Dequeue(), out var m)) continue;
            var needs = new List<(string Id, string What)>();
            foreach (var r in m.Requirements)
                if (r.ProviderPackageId is { } pid && r.Tone != Tone.Ok) needs.Add((pid, r.Name));
            foreach (var r in m.RequirementLinks.Where(r => !r.External && !r.Optional && r.ModId is not null))
                foreach (var x in o.Mods.Where(x => NexusInfo.IdOf(x.Package).Id == r.ModId)) needs.Add((x.Package.PackageId, r.Name));
            foreach (var (id, what) in needs)
            {
                if (!byId.TryGetValue(id, out var provider) || provider.Wanted || !seen.Add(id)) continue;
                result.Add(new InactiveRequirement(id, provider.Package.ModName, m.Package.ModName, what));
                queue.Enqueue(id);
            }
        }
        return result;
    }

    /// <summary>
    /// P54: mods switched on in the selected profile that need one of the given mods (directly or through another
    /// such mod), from files or the Nexus page. Only when no other switched-on mod provides the same thing.
    /// </summary>
    public static IReadOnlyList<InactiveRequirement> ActiveDependents(InstallOverview o, IEnumerable<string> packageIds)
    {
        var turningOff = packageIds.ToHashSet(StringComparer.Ordinal);
        var result = new List<InactiveRequirement>();
        var changed = true;
        while (changed)
        {
            changed = false;
            var offNexus = o.Mods.Where(m => turningOff.Contains(m.Package.PackageId)).Select(m => NexusInfo.IdOf(m.Package).Id).OfType<string>().ToHashSet();
            var stillOn = o.Mods.Where(m => m.Wanted && !turningOff.Contains(m.Package.PackageId)).ToList();
            foreach (var m in stillOn)
            {
                string? why = null;
                foreach (var r in m.Requirements)
                {
                    if (r.ProviderPackageId is not { } pid || !turningOff.Contains(pid)) continue;
                    var other = stillOn.Any(x => x.Package.PackageId != m.Package.PackageId && x.Package.Provides.Contains(r.Capability));
                    if (!other) { why = $"{r.Name} ({o.Mods.First(x => x.Package.PackageId == pid).Package.ModName})"; break; }
                }
                if (why is null)
                    foreach (var r in m.RequirementLinks.Where(r => !r.External && !r.Optional && r.ModId is not null && offNexus.Contains(r.ModId)))
                    {
                        var other = stillOn.Any(x => NexusInfo.IdOf(x.Package).Id == r.ModId);
                        if (!other) { why = r.Name; break; }
                    }
                if (why is null) continue;
                turningOff.Add(m.Package.PackageId);
                result.Add(new InactiveRequirement(m.Package.PackageId, m.Package.ModName, why, why));
                changed = true;
            }
        }
        return result;
    }

    /// <summary>
    /// P81: switched-on mods the given mods need (files or Nexus page) that nothing else switched on needs once they
    /// are off. Core mods are never offered.
    /// </summary>
    public static IReadOnlyList<InactiveRequirement> UnneededRequirements(InstallOverview o, IEnumerable<string> turningOffIds)
    {
        var off = turningOffIds.ToHashSet(StringComparer.Ordinal);
        var byId = o.Mods.ToDictionary(m => m.Package.PackageId, StringComparer.Ordinal);
        var candidates = new Dictionary<string, string>(StringComparer.Ordinal); // id -> needed by
        foreach (var id in off)
        {
            if (!byId.TryGetValue(id, out var m)) continue;
            foreach (var r in m.Requirements)
                if (r.ProviderPackageId is { } pid) candidates.TryAdd(pid, m.Package.ModName);
            foreach (var r in m.RequirementLinks.Where(r => !r.External && r.ModId is not null))
                foreach (var x in o.Mods.Where(x => NexusInfo.IdOf(x.Package).Id == r.ModId)) candidates.TryAdd(x.Package.PackageId, m.Package.ModName);
        }
        var baseline = ActiveDependents(o, off).Select(d => d.PackageId).ToHashSet(StringComparer.Ordinal);
        var result = new List<InactiveRequirement>();
        foreach (var (id, by) in candidates)
        {
            if (off.Contains(id) || !byId.TryGetValue(id, out var c) || !c.Wanted || c.IsCore) continue;
            var with = ActiveDependents(o, off.Append(id)).Select(d => d.PackageId).Where(x => x != id);
            if (with.All(baseline.Contains)) result.Add(new InactiveRequirement(id, c.Package.ModName, by, by));
        }
        return result;
    }

    public static InstallOverview Build(InstallCoordinator c)
    {
        var status = c.Status();
        var preview = c.Preview();
        var snapshot = preview.Snapshot;
        var profile = status.Selected;
        var packages = c.Store.GetPackages();
        var collections = CollectionStore.List(c.Store);
        var byId = packages.ToDictionary(p => p.PackageId);
        var ledger = c.Store.GetLedger();
        var ledgerOwners = ledger.Values.GroupBy(l => l.Owner).ToDictionary(g => g.Key, g => g.Count());
        var lastIncluded = status.LastGeneration is null || status.Unfinished is not null
            ? new HashSet<string>()
            : Json.Deserialize<DeploymentSnapshot>(status.LastGeneration.SnapshotJson).IncludedPackages.ToHashSet();
        var included = snapshot.IncludedPackages.ToHashSet();
        var external = preview.Scan.ExternalCapabilities;

        var profiles = c.Profiles
            .OrderBy(p => p.IsSystemModsOff)
            .Select(p => new ProfileSummary(p.Id, p.Name, p.IsSystemModsOff, p.Entries.Count(e => e.Enabled), p.Id == profile.Id,
                status.Unfinished is null && status.LastGeneration?.ProfileId == p.Id))
            .ToList();

        var blockersByPath = preview.Plan.Blockers.Where(b => b.Path is not null)
            .GroupBy(b => GamePath.Parse(b.Path!).Key).ToDictionary(g => g.Key, g => g.ToList());
        var stepsByOwner = preview.Plan.Steps
            .Select(s => (s, owner: s.Kind == StepKind.Remove || s.Kind == StepKind.Forget
                ? (ledger.TryGetValue(GamePath.Parse(s.Path).Key, out var le) ? le.Owner : null)
                : s.Owner))
            .Where(x => x.owner is not null)
            .GroupBy(x => x.owner!).ToDictionary(g => g.Key, g => g.Select(x => x.s).ToList());

        var mods = new List<ModOverview>();
        var entryIndex = profile.Entries.Select((e, i) => (e.PackageId, i)).ToDictionary(x => x.PackageId, x => x.i);
        int OrderOf(PackageManifest p)
        {
            var id = p.LayerOfModId is null ? p.PackageId
                : packages.FirstOrDefault(b => b.ModId == p.LayerOfModId && b.LayerOfModId is null)?.PackageId ?? p.PackageId;
            return entryIndex.TryGetValue(id, out var i) ? i : int.MaxValue;
        }
        var custom = profile.ArchiveOrder.Custom;
        var sorted = packages
            .OrderBy(p => p.Provides.Any(c => c.StartsWith("framework:", StringComparison.Ordinal)) ? 0 : 1)
            .ThenBy(p => custom ? OrderOf(p) : 0)
            .ThenBy(p => p.LayerOfModId is null ? p.ModName : ModNameOf(packages, p.LayerOfModId) + " ~", StringComparer.OrdinalIgnoreCase);
        foreach (var p in sorted)
        {
            var wanted = profile.IsEnabled(p.PackageId);
            var inGame = ledgerOwners.ContainsKey(p.PackageId) || lastIncluded.Contains(p.PackageId);
            var isIncluded = included.Contains(p.PackageId);
            var issues = snapshot.Issues.Where(i => i.PackageId == p.PackageId).ToList();
            var myPaths = p.Files.Select(f => GamePath.Parse(f.Target).Key).ToHashSet();
            var ownedPaths = ledger.Values.Where(l => l.Owner == p.PackageId).Select(l => GamePath.Parse(l.Path).Key);
            var pathBlockers = myPaths.Concat(ownedPaths).Distinct()
                .SelectMany(k => blockersByPath.TryGetValue(k, out var l) ? l : []).ToList();
            issues.AddRange(pathBlockers);
            stepsByOwner.TryGetValue(p.PackageId, out var mySteps);
            mySteps ??= [];
            var hasOverride = profile.Overrides.Any(o => o.PackageId == p.PackageId);

            var (state, label, tone, explanation) = Classify(p, wanted, inGame, isIncluded, issues, mySteps, profile.IsSystemModsOff);
            mods.Add(new ModOverview(p, wanted, inGame, isIncluded, state, label, tone, hasOverride, explanation,
                Requirements(p, profile, packages, included, external, ledgerOwners, NexusInfo.Load(c.Store, NexusInfo.IdOf(p).Id)),
                Conflicts(p, snapshot, issues, byId, profile),
                InstallMap(p), issues)
            {
                NexusId = NexusInfo.IdOf(p).Id,
                NexusConfirmed = NexusInfo.IdOf(p).Confirmed,
                Nexus = NexusInfo.Load(c.Store, NexusInfo.IdOf(p).Id),
                Update = NexusUpdates.Load(c.Store, p.PackageId),
                Collections = collections.Where(r => r.PackageOf.Values.Contains(p.PackageId)).Select(r => r.Name).ToList(),
                CollectionOptional = collections.Any(r => r.Revision.Items.Any(i => i.Optional && r.PackageOf.TryGetValue(i.Key, out var x) && x == p.PackageId))
                    && !collections.Any(r => r.Revision.Items.Any(i => !i.Optional && r.PackageOf.TryGetValue(i.Key, out var x) && x == p.PackageId)),
                OrderLocked = collections.Any(r => r.OrderLocked && r.ProfileId == profile.Id && r.PackageOf.Values.Contains(p.PackageId)),
                RequirementLinks = NexusLinks(NexusInfo.Load(c.Store, NexusInfo.IdOf(p).Id), packages, ledgerOwners, external),
            });
        }

        var frameworks = Frameworks(snapshot, byId, external, ledgerOwners, preview.Scan.ExternalVersions);
        var archiveNames = snapshot.Files.Select(f => GamePath.Parse(f.Path))
            .Where(g => string.Equals(g.ParentValue, "archive/pc/mod", StringComparison.OrdinalIgnoreCase) && g.Extension == ".archive")
            .Select(g => g.FileName).ToList();

        var (canLaunch, hint) = LaunchState(status, preview);
        return new InstallOverview(status, profiles, mods, preview, frameworks, preview.Scan.Unmanaged, archiveNames, canLaunch, hint, Summary(preview.Plan))
        {
            RemovedBatches = c.Removed.List(),
            CoreMods = CoreMods(byId, external, ledgerOwners, preview.Scan.ExternalVersions),
            OrderIsCustom = custom,
        };
    }

    private static string ModNameOf(IReadOnlyList<PackageManifest> all, string modId) =>
        all.FirstOrDefault(p => p.ModId == modId && p.LayerOfModId is null)?.ModName ?? modId;

    private static (ModState, string, Tone, string) Classify(PackageManifest p, bool wanted, bool inGame, bool included,
        List<Issue> issues, List<PlanStep> steps, bool modsOff)
    {
        // Priority: safety blockers → outside change awaiting decision → pending operation → normal state.
        if (wanted && issues.Any(i => i.Kind == IssueKind.MissingRequirement && i.Severity == Severity.Blocking))
            return (ModState.MissingRequirement, "× MISSING REQUIREMENT", Tone.Block, "Wanted in this profile, but held back until its requirement is available.");
        var foreign = issues.Where(i => i.Kind == IssueKind.ForeignFileAtTarget && i.Path is not null).Select(i => i.Path!).Distinct().ToList();
        if (foreign.Count > 0)
            return (ModState.FileInTheWay, "× FILE IN THE WAY", Tone.Block,
                $"{foreign.Count} file(s) at this mod's paths are already in the game with other content and were not written by AFTERLIFE "
                + $"(for example left from a manual install): {string.Join(", ", foreign.Take(3))}{(foreign.Count > 3 ? $" +{foreign.Count - 3} more" : "")}. "
                + "Nothing was overwritten. REPLACE moves them aside (restorable in Scan manual mods); DISCARD IMPORT keeps them and drops this mod.");
        if (issues.Any(i => i.Kind == IssueKind.ModifiedManagedFile))
            return (ModState.ChangedOutside, "! CHANGED OUTSIDE · DECIDE", Tone.Warn, "Some of this mod's files were changed outside AFTERLIFE. Keep your edits or discard them before applying.");
        if (wanted && issues.Any(i => i.Kind == IssueKind.FileConflict))
            return (ModState.Conflict, "! CONFLICT · DECIDE", Tone.Warn, "Another mod provides the same file. Choose which one wins on the Conflicts tab.");
        if (wanted && !included && p.LayerOfModId is not null)
            return (ModState.UserLayerInactive, "– WAITING FOR MOD", Tone.Neutral, "Your captured edits apply only while their mod is enabled.");
        if (wanted && !included)
            return (ModState.HeldBack, "! HELD BACK", Tone.Warn, "Wanted in this profile, but not deployable right now.");
        if (wanted && included && !inGame)
            return (ModState.PendingAdd, "+ PENDING · APPLY", Tone.Info, "Will be added to the game when you apply.");
        if (wanted && included && steps.Count > 0)
            return (ModState.PendingUpdate, "↻ PENDING · APPLY", Tone.Info, "Changes for this mod are ready; apply to update the game.");
        if (!wanted && inGame)
            return (ModState.PendingRemoval, "− PENDING REMOVAL", Tone.Info, modsOff
                ? "Mods off removes every managed file when you apply."
                : "Will be removed from the game when you apply.");
        if (wanted && included && inGame)
            return (ModState.Deployed, "✓ DEPLOYED", Tone.Ok, "In the game and verified after the last apply.");
        return (ModState.Off, "– OFF", Tone.Neutral, "In the library, not in this profile.");
    }

    private static List<RequirementStatus> Requirements(PackageManifest p, ProfileState profile, IReadOnlyList<PackageManifest> all,
        HashSet<string> included, IReadOnlySet<string> external, Dictionary<string, int> ledgerOwners, NexusModInfo? page)
    {
        var list = new List<RequirementStatus>();
        foreach (var r in p.Requirements.Where(r => r.Mandatory))
        {
            var name = Capabilities.DisplayName(r.Capability);
            var providers = all.Where(x => x.Provides.Contains(r.Capability)).ToList();
            var includedProvider = providers.FirstOrDefault(x => included.Contains(x.PackageId));
            var overridden = profile.Overrides.Any(o => o.PackageId == p.PackageId && o.Capability == r.Capability);
            RequirementStatus s;
            if (external.Contains(r.Capability))
                s = new(r.Capability, name, "installed manually", Tone.Ok, null, false);
            else if (includedProvider is not null)
                s = new(r.Capability, name, ledgerOwners.ContainsKey(includedProvider.PackageId) ? "deployed" : "in profile · apply", Tone.Ok, includedProvider.PackageId, false);
            else if (overridden)
                s = new(r.Capability, name, "overridden", Tone.Warn, null, false);
            else if (providers.Any(x => profile.IsEnabled(x.PackageId)))
                s = new(r.Capability, name, "held back", Tone.Warn, providers.First(x => profile.IsEnabled(x.PackageId)).PackageId, true);
            else if (providers.Count > 0)
                s = new(r.Capability, name, "in library · not enabled", Tone.Warn, providers[0].PackageId, true);
            else
                s = new(r.Capability, name, "not in library", Tone.Block, null, true);
            var nexusId = FrameworkCatalog.Find(r.Capability)?.NexusModId;
            var onPage = nexusId is not null && page is not null && page.Requirements.Any(x => x.ModId == nexusId);
            var source = r.Origin switch { "nexus" => "Nexus page", "content" => "scripts", _ => "files" };
            if (onPage && source != "Nexus page") source += " · Nexus page";
            list.Add(s with { Source = source, NexusUrl = nexusId is null ? null : $"https://www.nexusmods.com/{NxmLink.Cyberpunk}/mods/{nexusId}" });
        }
        return list;
    }

    private static List<ConflictInfo> Conflicts(PackageManifest p, DeploymentSnapshot snapshot, List<Issue> issues,
        Dictionary<string, PackageManifest> byId, ProfileState profile)
    {
        string Name(string id) => byId.TryGetValue(id, out var x) ? x.ModName : id;
        var list = new List<ConflictInfo>();
        foreach (var f in snapshot.Files.Where(f => f.Providers.Count > 1 && f.Providers.Contains(p.PackageId)))
            list.Add(new ConflictInfo(f.Path, Name(f.OwnerPackageId), f.OwnerPackageId == p.PackageId, f.Providers.Select(Name).ToList()));
        foreach (var i in issues.Where(i => i.Kind == IssueKind.FileConflict && i.Path is not null))
        {
            if (list.Any(x => string.Equals(x.Path, i.Path, StringComparison.OrdinalIgnoreCase))) continue;
            var key = GamePath.Parse(i.Path!).Key;
            var current = snapshot.Files.FirstOrDefault(f => GamePath.Parse(f.Path).Key == key);
            var providers = profile.Entries.Where(e => e.Enabled && byId.TryGetValue(e.PackageId, out var x) && x.Files.Any(ff => GamePath.Parse(ff.Target).Key == key))
                .Select(e => Name(e.PackageId)).ToList();
            list.Add(new ConflictInfo(i.Path!, current is null ? null : Name(current.OwnerPackageId), false, providers));
        }
        return list;
    }

    private static readonly string[] MapRoots =
    {
        "archive/pc/mod", "r6/scripts", "r6/tweaks", "r6/input", "bin/x64/plugins/cyber_engine_tweaks/mods", "red4ext/plugins", "red4ext", "bin/x64",
    };

    public static List<InstallMapLine> InstallMap(PackageManifest p)
    {
        return p.Files
            .GroupBy(f => MapRoots.FirstOrDefault(r => f.Target.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase)) ?? f.Target.Split('/')[0])
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new InstallMapLine(g.Key, string.Join(" · ", g
                .GroupBy(f => Path.GetExtension(f.Target).ToLowerInvariant())
                .OrderByDescending(x => x.Count())
                .Select(x => $"{x.Count()} {(x.Key.Length == 0 ? "file" : x.Key)}"))))
            .ToList();
    }

    private static List<FrameworkStatus> Frameworks(DeploymentSnapshot snapshot, Dictionary<string, PackageManifest> byId,
        IReadOnlySet<string> external, Dictionary<string, int> ledgerOwners, IReadOnlyDictionary<string, string?> externalVersions)
    {
        var list = new List<FrameworkStatus>();
        foreach (var contract in FrameworkCatalog.Contracts)
        {
            var name = ShortName(contract.Capability);
            var deployed = byId.Values.FirstOrDefault(p => p.Provides.Contains(contract.Capability) && ledgerOwners.ContainsKey(p.PackageId));
            if (deployed is not null) list.Add(new FrameworkStatus(name, deployed.Version, Tone.Ok));
            else if (external.Contains(contract.Capability))
                list.Add(new FrameworkStatus(name, externalVersions.TryGetValue(contract.Capability, out var v) && v is not null ? $"{v} · manual" : "manual", Tone.Neutral));
        }
        return list;
    }

    private static List<NexusRequirementStatus> NexusLinks(NexusModInfo? info, IReadOnlyList<PackageManifest> packages, Dictionary<string, int> ledgerOwners, IReadOnlySet<string> external)
    {
        if (info is null) return [];
        var list = new List<NexusRequirementStatus>();
        foreach (var r in info.Requirements)
        {
            if (FrameworkCatalog.CapabilityOfNexusId(r.ModId) is not null) continue; // P46: merged into the requirement list above
            if (r.External) { list.Add(new(r.Name, r.Url, "outside Nexus", Tone.Neutral, true, r.Notes)); continue; }
            var have = packages.FirstOrDefault(p => NexusInfo.IdOf(p).Id == r.ModId);
            var manual = FrameworkCatalog.CapabilityOfNexusId(r.ModId) is { } cap && external.Contains(cap);
            var (status, tone) = have is null ? (manual ? ("installed by hand", Tone.Ok) : ("not in library", Tone.Warn))
                : ledgerOwners.ContainsKey(have.PackageId) ? ("deployed", Tone.Ok) : ("in library · not deployed", Tone.Info);
            list.Add(new(r.Name, r.Url, status, tone, false, r.Notes) { ModId = r.ModId });
        }
        return list;
    }

    private static List<CoreModStatus> CoreMods(Dictionary<string, PackageManifest> byId, IReadOnlySet<string> external,
        Dictionary<string, int> ledgerOwners, IReadOnlyDictionary<string, string?> externalVersions)
    {
        var list = new List<CoreModStatus>();
        foreach (var c in FrameworkCatalog.Contracts)
        {
            var name = c.ShortName.Length > 0 ? c.ShortName : ShortName(c.Capability);
            var providers = byId.Values.Where(p => p.Provides.Contains(c.Capability)).ToList();
            var deployed = providers.FirstOrDefault(p => ledgerOwners.ContainsKey(p.PackageId));
            if (deployed is not null) list.Add(new CoreModStatus(c.Capability, name, deployed.Version, CoreSource.InGame, c.NexusUrl, c.Optional, deployed.PackageId));
            else if (external.Contains(c.Capability))
                list.Add(new CoreModStatus(c.Capability, name, externalVersions.TryGetValue(c.Capability, out var v) ? v : null, CoreSource.Manual, c.NexusUrl, c.Optional, null));
            else if (providers.Count > 0)
                list.Add(new CoreModStatus(c.Capability, name, providers[0].Version, CoreSource.Library, c.NexusUrl, c.Optional, providers[0].PackageId));
            else list.Add(new CoreModStatus(c.Capability, name, null, CoreSource.Missing, c.NexusUrl, c.Optional, null));
        }
        return list;
    }

    public static string ShortName(string capability) => capability switch
    {
        Capabilities.Cet => "CET",
        Capabilities.Red4ext => "RED4ext",
        Capabilities.Redscript => "redscript",
        Capabilities.ArchiveXl => "ArchiveXL",
        Capabilities.TweakXl => "TweakXL",
        Capabilities.Codeware => "Codeware",
        Capabilities.InputLoader => "Input Loader",
        _ => Capabilities.DisplayName(capability),
    };

    private static (bool, string) LaunchState(StatusReport status, PreviewResult preview)
    {
        if (status.Unfinished is not null) return (false, "finish recovery first");
        if (preview.EnvironmentIssues.Any(i => i.Kind == IssueKind.GameRunning)) return (false, "the game is already running");
        if (status.Fingerprint is null) return (false, "game executable not found");
        if (status.LastGeneration?.ProfileId != status.Selected.Id) return (false, "apply this profile first");
        if (!preview.Plan.IsNoop) return (false, "apply pending changes first");
        if (status.BuildChanged) return (false, "game updated · review before modded launch");
        return (true, status.Selected.IsSystemModsOff ? "vanilla game, no managed mods" : $"{status.Selected.Name} is in the game");
    }

    private static string Summary(Plan plan)
    {
        if (plan.IsNoop) return "Game matches this profile";
        var add = plan.Steps.Count(s => s.Kind == StepKind.Add);
        var rep = plan.Steps.Count(s => s.Kind == StepKind.Replace);
        var rem = plan.Steps.Count(s => s.Kind == StepKind.Remove);
        var meta = plan.Steps.Count(s => s.Kind is StepKind.Forget or StepKind.Retag);
        var parts = new List<string>();
        if (add > 0) parts.Add($"{add} add");
        if (rep > 0) parts.Add($"{rep} replace");
        if (rem > 0) parts.Add($"{rem} remove");
        if (meta > 0 && parts.Count == 0) parts.Add($"{meta} bookkeeping");
        return $"{plan.Steps.Count} file change{(plan.Steps.Count == 1 ? "" : "s")} ready: {string.Join(" · ", parts)}";
    }
}
