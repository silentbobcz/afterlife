namespace Afterlife.Core;

public sealed record DesiredFile(string Path, string Hash, long Size, string OwnerPackageId, string StoredPath, IReadOnlyList<string> Providers);

public sealed record ModlistTarget(IReadOnlyList<string> ArchiveNames, string Content, string Hash);

/// <summary>Frozen target of one deployment operation. Never edited after creation.</summary>
public sealed record DeploymentSnapshot(
    string Id,
    string ProfileId,
    long ProfileRevision,
    IReadOnlyList<DesiredFile> Files,
    IReadOnlyList<string> IncludedPackages,
    IReadOnlyList<string> ExcludedPackages,
    ModlistTarget? Modlist,
    bool ModlistAllowTakeover,
    IReadOnlyList<Issue> Issues)
{
    public bool HasOperationBlockers => Issues.Any(i => i.Severity == Severity.Blocking && i.PackageId is null);
}

public static class ModlistFormat
{
    public const string RelativePath = "archive/pc/mod/modlist.txt";
    /// <summary>Line ending and encoding must be confirmed in M0 on game build 2.5.</summary>
    public const string NewLine = "\r\n";

    public static string Render(IEnumerable<string> archiveNames) =>
        string.Concat(archiveNames.Select(n => n + NewLine));
}

public sealed record SnapshotInputs(
    ProfileState Profile,
    IReadOnlyDictionary<string, PackageManifest> Packages,
    IReadOnlySet<string> ExternalCapabilities,
    IReadOnlyDictionary<string, string> IncumbentOwners,
    IReadOnlyList<string> UnmanagedArchiveNames);

/// <summary>
/// Computes the desired game tree from a profile. Packages that cannot be deployed (missing requirement,
/// unresolved file conflict) are excluded with a package-scoped blocking issue; the rest still deploys.
/// </summary>
public static class SnapshotBuilder
{
    public static DeploymentSnapshot Build(SnapshotInputs input, string snapshotId)
    {
        var profile = input.Profile;
        var issues = new List<Issue>();

        if (profile.IsSystemModsOff)
            return new DeploymentSnapshot(snapshotId, profile.Id, profile.Revision, [], [], [], null, false, []);

        var candidates = new List<PackageManifest>();
        foreach (var e in profile.Entries.Where(e => e.Enabled))
        {
            if (input.Packages.TryGetValue(e.PackageId, out var pkg)) candidates.Add(pkg);
            else issues.Add(new Issue(IssueKind.MissingPackage, Severity.Blocking, $"package {e.PackageId} is missing from the library", e.PackageId));
        }

        var excluded = new Dictionary<string, Issue>();
        bool changed = true;
        while (changed)
        {
            changed = false;
            var active = candidates.Where(p => !excluded.ContainsKey(p.PackageId)).ToList();

            // User layers (captured changes) only apply while their base mod is active.
            foreach (var layer in active.Where(p => p.LayerOfModId is not null))
            {
                if (!active.Any(p => p.LayerOfModId is null && p.ModId == layer.LayerOfModId))
                {
                    excluded[layer.PackageId] = new Issue(IssueKind.MissingRequirement, Severity.Info,
                        "user changes apply only while their mod is enabled", layer.PackageId);
                    changed = true;
                }
            }
            if (changed) continue;

            var provided = new HashSet<string>(input.ExternalCapabilities, StringComparer.Ordinal);
            foreach (var p in active) foreach (var c in p.Provides) provided.Add(c);

            foreach (var p in active)
            {
                foreach (var r in p.Requirements.Where(r => r.Mandatory))
                {
                    if (provided.Contains(r.Capability)) continue;
                    if (profile.Overrides.Any(o => o.PackageId == p.PackageId && o.Capability == r.Capability)) continue;
                    excluded[p.PackageId] = new Issue(IssueKind.MissingRequirement, Severity.Blocking,
                        $"missing requirement: {Capabilities.DisplayName(r.Capability)}", p.PackageId, Capability: r.Capability);
                    changed = true;
                    break;
                }
            }
            if (changed) continue;

            // Direct path conflicts.
            var groups = active
                .SelectMany(p => p.Files.Select(f => (Pkg: p, File: f, Key: f.Target.ToUpperInvariant())))
                .GroupBy(x => x.Key, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal);
            foreach (var g in groups)
            {
                if (g.Select(x => x.File.Hash).Distinct().Count() <= 1) continue;
                var pkgIds = g.Select(x => x.Pkg.PackageId).Distinct().ToList();
                if (profile.PathWinners.TryGetValue(g.Key, out var ruleWinner) && pkgIds.Contains(ruleWinner)) continue;

                if (input.IncumbentOwners.TryGetValue(g.Key, out var incumbent) && pkgIds.Contains(incumbent))
                {
                    var incumbentHash = g.First(x => x.Pkg.PackageId == incumbent).File.Hash;
                    foreach (var x in g.Where(x => x.Pkg.PackageId != incumbent && x.File.Hash != incumbentHash))
                    {
                        if (excluded.ContainsKey(x.Pkg.PackageId)) continue;
                        excluded[x.Pkg.PackageId] = new Issue(IssueKind.FileConflict, Severity.Blocking,
                            $"file conflict on {x.File.Target}; choose a winner in Priority & conflicts", x.Pkg.PackageId, x.File.Target);
                        changed = true;
                    }
                }
                else
                {
                    foreach (var x in g)
                    {
                        if (excluded.ContainsKey(x.Pkg.PackageId)) continue;
                        excluded[x.Pkg.PackageId] = new Issue(IssueKind.FileConflict, Severity.Blocking,
                            $"file conflict on {x.File.Target}; choose a winner in Priority & conflicts", x.Pkg.PackageId, x.File.Target);
                        changed = true;
                    }
                }
                if (changed) break;
            }
        }

        var included = candidates.Where(p => !excluded.ContainsKey(p.PackageId)).ToList();
        issues.AddRange(excluded.Values.Where(i => i.Severity != Severity.Info));

        // Warnings for included packages.
        var finalProvided = new HashSet<string>(input.ExternalCapabilities, StringComparer.Ordinal);
        foreach (var p in included) foreach (var c in p.Provides) finalProvided.Add(c);
        foreach (var p in included)
        {
            foreach (var r in p.Requirements.Where(r => r.Mandatory && !finalProvided.Contains(r.Capability)))
                issues.Add(new Issue(IssueKind.EnabledWithOverride, Severity.Warning,
                    $"enabled with override: {Capabilities.DisplayName(r.Capability)} is missing", p.PackageId, Capability: r.Capability));
            // P36/P38: frameworks install like any other mod; no "unverified" warning any more.
        }

        // Desired files.
        var order = included.Select((p, i) => (p.PackageId, i)).ToDictionary(x => x.PackageId, x => x.i);
        var files = new List<DesiredFile>();
        foreach (var g in included
                     .SelectMany(p => p.Files.Select(f => (Pkg: p, File: f)))
                     .GroupBy(x => x.File.Target.ToUpperInvariant(), StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            (PackageManifest Pkg, PackageFile File) winner;
            if (profile.PathWinners.TryGetValue(g.Key, out var w) && g.Any(x => x.Pkg.PackageId == w))
                winner = g.First(x => x.Pkg.PackageId == w);
            else if (input.IncumbentOwners.TryGetValue(g.Key, out var inc) && g.Any(x => x.Pkg.PackageId == inc))
                winner = g.First(x => x.Pkg.PackageId == inc);
            else
                winner = g.OrderBy(x => order[x.Pkg.PackageId]).First();
            var providers = g.Where(x => x.File.Hash == winner.File.Hash).Select(x => x.Pkg.PackageId).Distinct().ToList();
            files.Add(new DesiredFile(winner.File.Target, winner.File.Hash, winner.File.Size, winner.Pkg.PackageId, winner.File.StoredPath, providers));
        }

        // Archive order (modlist.txt) only when the profile explicitly asks for it.
        ModlistTarget? modlist = null;
        var ao = profile.ArchiveOrder;
        if (ao.Custom)
        {
            var managedArchives = files
                .Select(f => GamePath.Parse(f.Path))
                .Where(p => p.ParentValue is not null && string.Equals(p.ParentValue, "archive/pc/mod", StringComparison.OrdinalIgnoreCase) && p.Extension == ".archive")
                .Select(p => p.FileName)
                .ToList();
            var unmanaged = input.UnmanagedArchiveNames
                .Where(n => !managedArchives.Contains(n, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (unmanaged.Count > 0 && !ao.IncludeUnmanagedArchives)
            {
                issues.Add(new Issue(IssueKind.UnmanagedArchivesInCustomOrder, Severity.Blocking,
                    $"{unmanaged.Count} .archive file(s) installed outside AFTERLIFE would be reordered by your load order; confirm including them or remove them"));
            }
            else
            {
                var all = managedArchives.Concat(unmanaged).ToList();
                var ordered = new List<string>();
                foreach (var name in ao.Order)
                {
                    var match = all.FirstOrDefault(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
                    if (match is not null && !ordered.Contains(match)) ordered.Add(match);
                }
                ordered.AddRange(all.Where(a => !ordered.Contains(a)).Order(StringComparer.Ordinal));
                var content = ModlistFormat.Render(ordered);
                modlist = new ModlistTarget(ordered, content, ContentHash.Of(content));
            }
        }

        if (files.Any(f => f.Path.EndsWith(".archive", StringComparison.OrdinalIgnoreCase)))
            issues.Add(new Issue(IssueKind.ArchiveContentsNotAnalyzed, Severity.Info, "archive contents not analyzed"));

        return new DeploymentSnapshot(snapshotId, profile.Id, profile.Revision, files,
            included.Select(p => p.PackageId).ToList(), excluded.Keys.ToList(), modlist, ao.AllowModlistTakeover, issues);
    }
}
