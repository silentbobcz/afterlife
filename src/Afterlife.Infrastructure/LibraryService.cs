using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>Packages in the library: import, names, duplicates, new versions, deletion, requirements and storage.</summary>
public sealed class LibraryService
{
    private readonly InstallContext _ctx;
    private AfterlifeStore Store => _ctx.Store;
    private LibraryPaths Library => _ctx.LibraryPaths;
    private PackageLibrary Packages => _ctx.Packages;
    private Quarantine Removed => _ctx.Removed;
    private IFileOps _fs => _ctx.Fs;
    private string GameRoot => _ctx.GameRoot;
    private ProfileState SelectedProfile => _ctx.SelectedProfile;

    internal LibraryService(InstallContext ctx) => _ctx = ctx;

    /// <summary>Shown name of a mod in AFTERLIFE; files in the game do not change. Empty = back to the original name.</summary>
    public PackageManifest RenamePackage(string packageId, string? name)
    {
        var p = Store.GetPackage(packageId) ?? throw new InvalidOperationException($"unknown package {packageId}");
        var key = "name.original:" + packageId;
        var original = Store.GetMeta(key) ?? p.ModName;
        var target = string.IsNullOrWhiteSpace(name) ? original : name.Trim();
        if (Store.GetMeta(key) is null && target != p.ModName) Store.SetMeta(key, p.ModName);
        var updated = p with { ModName = target };
        Store.UpdatePackage(updated);
        return updated;
    }

    public string OriginalName(string packageId) => Store.GetMeta("name.original:" + packageId) ?? Store.GetPackage(packageId)?.ModName ?? "";

    /// <summary>
    /// An archive whose files are exactly those of a package already in the library (same digest:
    /// Paths, hashes and sizes) does not become a second copy; the existing package is returned instead.
    /// </summary>
    public ImportResult Import(ImportRequest request)
    {
        var r = Packages.Import(request);
        ActivityLog.Write("import", r.Package is null
            ? $"{Path.GetFileName(request.ArchivePath)} rejected: {string.Join("; ", r.Analysis.Problems)}"
            : $"{Path.GetFileName(request.ArchivePath)} → {r.Package.ModName} {r.Package.Version} [{r.Package.PackageId}], {r.Package.Files.Count} file(s)");
        if (r.Package is not { } p) return r;
        var same = Store.GetPackages()
            .Where(x => x.PackageId != p.PackageId && x.LayerOfModId is null && x.ManifestDigest == p.ManifestDigest)
            .OrderBy(x => x.ImportedAt ?? DateTimeOffset.MinValue).FirstOrDefault();
        if (same is null) return r;
        Packages.Delete(p.PackageId);
        return r with { Package = same, AlreadyInLibrary = true };
    }

    /// <summary>Groups of packages with exactly the same files (duplicates made before 0.5.3).</summary>
    public IReadOnlyList<IReadOnlyList<PackageManifest>> DuplicateGroups() =>
        Store.GetPackages().Where(p => p.LayerOfModId is null && p.Files.Count > 0)
            .GroupBy(p => p.ManifestDigest)
            .Where(g => g.Count() > 1)
            .Select(g => (IReadOnlyList<PackageManifest>)g.OrderBy(p => p.ImportedAt ?? DateTimeOffset.MinValue).ToList())
            .ToList();

    /// <summary>
    /// The kept package takes the place of its duplicates: in every profile (on when any copy was on,
    /// At the position of the first copy) and in every collection. The duplicates are then free to delete.
    /// </summary>
    public void AdoptDuplicates(string keepId, IReadOnlyCollection<string> others)
    {
        var all = others.Append(keepId).ToHashSet(StringComparer.Ordinal);
        Store.InTransaction(() =>
        {
            foreach (var p in Store.GetProfiles().Where(p => !p.IsSystemModsOff))
            {
                var entries = p.Entries.ToList();
                var first = entries.FindIndex(e => all.Contains(e.PackageId));
                if (first < 0) continue;
                var on = entries.Any(e => all.Contains(e.PackageId) && e.Enabled);
                entries.RemoveAll(e => all.Contains(e.PackageId));
                entries.Insert(Math.Min(first, entries.Count), new ProfileEntry(keepId, on));
                Store.SaveProfile(p with { Entries = entries });
            }
            foreach (var r in CollectionStore.List(Store))
            {
                if (!r.PackageOf.Values.Any(others.Contains)) continue;
                var map = r.PackageOf.ToDictionary(kv => kv.Key, kv => others.Contains(kv.Value) ? keepId : kv.Value);
                CollectionStore.Save(Store, r with { PackageOf = map });
            }
        });
    }

    /// <summary>
    /// The new version takes the place of the old one everywhere: in every profile (same position, same on/off),
    /// As the winner of its file conflicts, in its requirement overrides and in every collection. The old version is
    /// Then in no profile and can be deleted from the library once the game no longer has its files.
    /// </summary>
    public void ReplaceVersion(string oldId, string newId)
    {
        if (Store.GetPackage(newId) is null) throw new InvalidOperationException($"unknown package {newId}");
        ActivityLog.Write("update", $"{Store.GetPackage(oldId)?.ModName ?? oldId} [{oldId}] → [{newId}] in every profile and collection");
        Store.InTransaction(() =>
        {
            foreach (var p in Store.GetProfiles().Where(p => !p.IsSystemModsOff))
            {
                var entries = p.Entries.ToList();
                var at = entries.FindIndex(e => e.PackageId == oldId);
                var winners = p.PathWinners.Any(kv => kv.Value == oldId);
                var overrides = p.Overrides.Any(o => o.PackageId == oldId);
                if (at < 0 && !winners && !overrides) continue;
                var q = p;
                if (at >= 0)
                {
                    var on = entries[at].Enabled || entries.Any(e => e.PackageId == newId && e.Enabled);
                    entries.RemoveAll(e => e.PackageId == newId);
                    at = entries.FindIndex(e => e.PackageId == oldId);
                    entries[at] = new ProfileEntry(newId, on);
                    q = q with { Entries = entries };
                }
                if (winners)
                    q = q with { PathWinners = p.PathWinners.ToDictionary(kv => kv.Key, kv => kv.Value == oldId ? newId : kv.Value) };
                if (overrides)
                    q = q with { Overrides = p.Overrides.Select(o => o.PackageId == oldId ? o with { PackageId = newId } : o).Distinct().ToList() };
                Store.SaveProfile(q with { Revision = p.Revision + 1 });
            }
            ReplaceInCollections(oldId, newId);
        });
    }

    /// <summary>The new version of a mod takes the place of the old one in every collection.</summary>
    public void ReplaceInCollections(string oldId, string newId)
    {
        foreach (var r in CollectionStore.List(Store))
        {
            if (!r.PackageOf.Values.Contains(oldId)) continue;
            var map = r.PackageOf.ToDictionary(kv => kv.Key, kv => kv.Value == oldId ? newId : kv.Value);
            CollectionStore.Save(Store, r with { PackageOf = map });
        }
    }

    /// <summary>Removes a package that no profile wants: drops its disabled entries, then deletes it from the library.</summary>
    public void RemoveFromLibrary(string packageId)
    {
        var wanted = Store.GetProfiles().Where(p => p.IsEnabled(packageId)).Select(p => p.Name).ToList();
        if (wanted.Count > 0) throw new InvalidOperationException($"switch it off first in: {string.Join(", ", wanted)}");
        foreach (var p in Store.GetProfiles().Where(p => !p.IsSystemModsOff && p.Entries.Any(e => e.PackageId == packageId)))
            Store.SaveProfile(p.WithoutEntry(packageId));
        Packages.Delete(packageId);
    }

    /// <summary>Stores changed managed files as a user layer of their mod and makes the layer win those paths.</summary>
    /// <summary>
    /// Keeps the user's edits of managed files as a layer package. The layer is enabled in the selected profile
    /// (or, when Mods off is selected, in the profile that is deployed) and takes over the ledger entries,
    /// Because the files on disk are now exactly the layer's content.
    /// </summary>
    public PackageManifest CaptureChanges(IReadOnlyCollection<string> paths)
    {
        var target = SelectedProfile;
        if (target.IsSystemModsOff)
        {
            var deployedId = Store.GetLastGeneration()?.ProfileId;
            target = (deployedId is null ? null : Store.GetProfile(deployedId)) ?? target;
            if (target.IsSystemModsOff) throw new InvalidOperationException("select the profile these edits belong to, then capture them");
        }
        var ledger = Store.GetLedger();
        var byOwner = paths.Select(p => GamePath.Parse(p))
            .GroupBy(p => ledger.TryGetValue(p.Key, out var le) ? le.Owner : throw new InvalidOperationException($"{p} is not managed"));
        PackageManifest? last = null;
        foreach (var g in byOwner)
        {
            var layer = Packages.CaptureLayer(GameRoot, g.Key, g.Select(p => p.Value), _fs);
            var q = (Store.GetProfile(target.Id) ?? target).WithEntry(layer.PackageId, true);
            foreach (var f in layer.Files) q = q.WithWinner(f.Target.ToUpperInvariant(), layer.PackageId);
            SaveLayerAndRetag(q, layer);
            last = layer;
        }
        return last ?? throw new InvalidOperationException("nothing to capture");
    }

    private void SaveLayerAndRetag(ProfileState profile, PackageManifest layer)
    {
        Store.InTransaction(() =>
        {
            Store.SaveProfile(profile);
            foreach (var f in layer.Files)
            {
                var gp = GamePath.Parse(f.Target);
                var os = gp.ToOsPath(GameRoot);
                var meta = _fs.GetMeta(os);
                if (meta is null || _fs.Hash(os) != f.Hash) continue; // changed again since capture: leave it to the planner
                Store.UpsertLedger(new LedgerEntry(gp.Value, ArtifactKind.Payload, layer.PackageId, f.Hash, meta.Size, meta.MTimeTicks));
            }
        });
    }

    /// <summary>
    /// Switches packages off in every profile and deletes those that are no longer needed. Packages whose files are
    /// Still in the game are kept and reported (the window uses <see cref="DeleteEverywhere"/> instead).
    /// </summary>
    public (IReadOnlyList<string> Deleted, IReadOnlyList<(string Name, string Reason)> Kept) DeleteFromLibrary(IReadOnlyCollection<string> packageIds)
    {
        var ids = packageIds.ToHashSet();
        foreach (var p in Store.GetProfiles().Where(p => !p.IsSystemModsOff && p.Entries.Any(e => ids.Contains(e.PackageId))))
        {
            var q = p;
            foreach (var id in ids) q = q.WithoutEntry(id);
            Store.SaveProfile(q);
        }
        var deleted = new List<string>();
        var kept = new List<(string, string)>();
        foreach (var id in ids)
        {
            var name = Store.GetPackage(id)?.ModName ?? id;
            try { Packages.Delete(id); deleted.Add(name); }
            catch (InvalidOperationException ex)
            {
                kept.Add((name, ex.Message.Contains("deployed") ? "still in the game; apply first, then delete" : ex.Message));
            }
        }
        return (deleted, kept);
    }

    /// <summary>
    /// One confirmed action. Switches the packages off in every profile, takes their files out of the game
    /// (re-deploying exactly what is in the game now minus these packages, so other pending edits stay pending)
    /// And deletes them from the library.
    /// </summary>
    public DeleteOutcome DeleteEverywhere(IReadOnlyCollection<string> packageIds, IProgress<DeployProgress>? progress = null)
    {
        using var _ = InstallLock.Acquire(Library, GameRoot);
        _ctx.EnsureQuiet();
        var ids = packageIds.ToHashSet();
        var names = ids.ToDictionary(id => id, id => Store.GetPackage(id)?.ModName ?? id);
        ActivityLog.Write("delete", $"delete everywhere: {string.Join(", ", names.Values)}");
        Store.InTransaction(() =>
        {
            foreach (var p in Store.GetProfiles().Where(p => !p.IsSystemModsOff && p.Entries.Any(e => ids.Contains(e.PackageId))))
            {
                var q = p;
                foreach (var id in ids) q = q.WithoutEntry(id);
                Store.SaveProfile(q);
            }
        });

        ExecutionResult? exec = null;
        var last = Store.GetLastGeneration();
        var deployedSnap = last is null ? null : Json.Deserialize<DeploymentSnapshot>(last.SnapshotJson);
        var inGame = Store.GetLedger().Values.Any(l => ids.Contains(l.Owner)) || (deployedSnap?.IncludedPackages.Any(ids.Contains) ?? false);
        if (inGame && last is not null && deployedSnap is not null)
        {
            var deployedProfile = Store.GetProfile(last.ProfileId);
            var keep = deployedSnap.IncludedPackages.Where(id => !ids.Contains(id)).ToHashSet();
            var target = deployedProfile is null || deployedProfile.IsSystemModsOff
                ? ProfileState.CreateModsOff()
                : deployedProfile with
                {
                    Entries = deployedProfile.Entries.Where(e => keep.Contains(e.PackageId)).Select(e => e with { Enabled = true }).ToList(),
                };
            var scan = _ctx.Scanner.Scan();
            var preview = _ctx.Deployer.MakePreview(_ctx.Deployer.BuildSnapshot(target, scan), scan, null);
            // A mod being deleted changed its own files (settings, saves): keep the changed copies in Removed and go on.
            var changed = preview.Plan.Blockers.Where(b => b.Kind == IssueKind.ModifiedManagedFile && b.Path is not null && b.PackageId is not null && ids.Contains(b.PackageId)).ToList();
            if (!preview.CanApply && changed.Count > 0
                && preview.Plan.Blockers.Count == changed.Count && !preview.EnvironmentIssues.Any(i => i.Severity == Severity.Blocking))
            {
                foreach (var g in changed.GroupBy(b => b.PackageId!))
                {
                    try { Removed.Remove(GameRoot, g.Select(b => GamePath.Parse(b.Path!)).ToList(), $"Changed files of {names[g.Key]}"); }
                    catch (IOException) { }
                }
                scan = _ctx.Scanner.Scan();
                preview = _ctx.Deployer.MakePreview(_ctx.Deployer.BuildSnapshot(target, scan), scan, null);
            }
            if (!preview.CanApply)
                return new DeleteOutcome([], ids.Select(id => (names[id], "could not be taken out of the game: " +
                    string.Join("; ", preview.Plan.Blockers.Concat(preview.EnvironmentIssues.Where(i => i.Severity == Severity.Blocking)).Select(i => i.Message)))).ToList(), null);
            if (!preview.Plan.IsNoop)
            {
                exec = _ctx.Deployer.Run(preview.Snapshot, preview.Plan, progress);
                if (exec.Status != OperationStatus.Verified)
                    return new DeleteOutcome([], ids.Select(id => (names[id], "taking it out of the game did not finish; run recovery")).ToList(), exec);
            }
        }

        var deleted = new List<string>();
        var kept = new List<(string, string)>();
        foreach (var id in ids)
        {
            try { Packages.Delete(id); deleted.Add(names[id]); }
            catch (InvalidOperationException ex) { kept.Add((names[id], ex.Message)); }
        }
        var gone = ids.Where(id => Store.GetPackage(id) is null).ToList();
        var leftovers = _ctx.Scanner.MoveRuntimeLeftovers(gone, names);
        return new DeleteOutcome(deleted, kept, exec) { Leftovers = leftovers.Sum(l => l.Moved) };
    }

    /// <summary>Managed mods that need one of these capabilities and are wanted somewhere or in the game.</summary>
    public IReadOnlyList<Dependent> DependentsOf(IReadOnlyCollection<string> capabilities, IReadOnlyCollection<string>? exceptPackages = null)
    {
        var caps = capabilities.ToHashSet(StringComparer.Ordinal);
        var except = exceptPackages?.ToHashSet() ?? [];
        var profiles = Store.GetProfiles().Where(p => !p.IsSystemModsOff).ToList();
        var owners = Store.GetLedger().Values.Select(l => l.Owner).ToHashSet();
        var list = new List<Dependent>();
        foreach (var p in Store.GetPackages())
        {
            if (except.Contains(p.PackageId)) continue;
            var needs = p.Requirements.Where(r => r.Mandatory && caps.Contains(r.Capability)).Select(r => r.Capability).Distinct().ToList();
            if (needs.Count == 0) continue;
            var inProfiles = profiles.Where(q => q.IsEnabled(p.PackageId)).Select(q => q.Name).ToList();
            var inGame = owners.Contains(p.PackageId);
            if (inProfiles.Count == 0 && !inGame) continue;
            list.Add(new Dependent(p.PackageId, p.ModName, needs, inProfiles, inGame));
        }
        return list.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Switches packages off in every profile (entries stay, so their place in the order is kept).</summary>
    public void DeactivateEverywhere(IReadOnlyCollection<string> packageIds)
    {
        var ids = packageIds.ToHashSet();
        Store.InTransaction(() =>
        {
            foreach (var p in Store.GetProfiles().Where(p => !p.IsSystemModsOff && p.Entries.Any(e => ids.Contains(e.PackageId) && e.Enabled)))
            {
                var q = p;
                foreach (var id in ids.Where(p.IsEnabled)) q = q.WithEntry(id, false);
                Store.SaveProfile(q);
            }
        });
    }

    /// <summary>
    /// Recomputes what every library package provides and needs with the current rules: paths, script content
    /// (Codeware, Mod Settings, RedFileSystem, Native Settings UI) and the mod's Nexus page when it is known.
    /// Old packages get new rules too. Returns the number of packages whose requirements changed.
    /// </summary>
    public int RefreshRequirements()
    {
        var changed = 0;
        foreach (var p in Store.GetPackages())
        {
            var targets = p.Files.Select(f => GamePath.Parse(f.Target)).ToList();
            var provides = FrameworkCatalog.DetectProvided(targets).Union(p.Provides.Where(c => !c.StartsWith("framework:", StringComparison.Ordinal))).ToList();
            var reqs = FrameworkCatalog.InferRequirements(targets, provides).ToList();
            var scripts = p.Files.Where(f => f.Size < 1_000_000 && (f.Target.EndsWith(".reds", StringComparison.OrdinalIgnoreCase) || f.Target.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)))
                .Select(f =>
                {
                    var path = Library.PackageFile(p.PackageId, f.StoredPath);
                    try { return (GamePath.Parse(f.Target), File.Exists(path) ? File.ReadAllText(path) : ""); }
                    catch (IOException) { return (GamePath.Parse(f.Target), ""); }
                });
            foreach (var r in FrameworkCatalog.InferFromContent(scripts, provides))
                if (reqs.All(x => x.Capability != r.Capability)) reqs.Add(r);
            if (NexusInfo.Load(Store, p.NexusModId) is { } info)
                foreach (var nr in info.Requirements)
                    if (FrameworkCatalog.CapabilityOfNexusId(nr.ModId) is { } cap && !provides.Contains(cap) && reqs.All(x => x.Capability != cap))
                        reqs.Add(new RequirementSpec(cap, !NexusInfo.IsOptionalNote(nr.Notes), "nexus", $"listed on the Nexus page → {Capabilities.DisplayName(cap)}"));
            reqs = reqs.OrderBy(r => r.Capability, StringComparer.Ordinal).ToList();
            var same = reqs.Select(r => r.Capability + r.Origin).SequenceEqual(p.Requirements.Select(r => r.Capability + r.Origin))
                && provides.OrderBy(x => x).SequenceEqual(p.Provides.OrderBy(x => x));
            if (same) continue;
            Store.UpdatePackage(p with { Requirements = reqs, Provides = provides });
            changed++;
        }
        return changed;
    }

    /// <summary>Links a library package to a mod on Nexus (id typed by the user or taken from a link).</summary>
    public PackageManifest LinkToNexus(string packageId, string nexusModId)
    {
        var p = Store.GetPackage(packageId) ?? throw new InvalidOperationException($"unknown package {packageId}");
        if (nexusModId.Length == 0 || !nexusModId.All(char.IsDigit)) throw new InvalidOperationException("a Nexus mod id is a number, for example 107");
        var q = p with { NexusModId = nexusModId, Origin = p.Origin == PackageOrigin.Captured ? p.Origin : PackageOrigin.Nexus };
        Store.UpdatePackage(q);
        return q;
    }

    public StorageInfo StorageInfo()
    {
        var removed = Directory.Exists(Removed.Root) ? Directory.EnumerateDirectories(Removed.Root).Count() : 0;
        var (dl, dlBytes) = Afterlife.Infrastructure.Storage.FilesIn(Library.Downloads);
        return new StorageInfo(removed, Afterlife.Infrastructure.Storage.SizeOf(Removed.Root), dl, dlBytes,
            Afterlife.Infrastructure.Storage.SizeOf(Library.Staging) + Afterlife.Infrastructure.Storage.SizeOf(Library.Backups));
    }

    /// <summary>Deletes kept files for good. Never touches the game, the mods in the library or an unfinished change.</summary>
    public long PurgeStorage(bool removed, bool downloads, bool temp)
    {
        using var _ = InstallLock.Acquire(Library, GameRoot);
        if (Store.GetUnfinishedOperation() is not null) throw new InvalidOperationException("finish or undo the interrupted change first");
        long freed = 0;
        if (removed) freed += Removed.PurgeAll();
        if (downloads) freed += Afterlife.Infrastructure.Storage.Empty(Library.Downloads);
        if (temp) freed += Afterlife.Infrastructure.Storage.Empty(Library.Staging) + Afterlife.Infrastructure.Storage.Empty(Library.Backups);
        return freed;
    }
}
