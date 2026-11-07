using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>The game folder as AFTERLIFE sees it: manual mods, files in the way, runtime files of core mods, and moving such files aside.</summary>
public sealed class ScanService
{
    private readonly InstallContext _ctx;
    private AfterlifeStore Store => _ctx.Store;
    private LibraryPaths Library => _ctx.LibraryPaths;
    private Quarantine Removed => _ctx.Removed;
    private IFileOps _fs => _ctx.Fs;
    private string GameRoot => _ctx.GameRoot;
    private ProfileState SelectedProfile => _ctx.SelectedProfile;

    internal ScanService(InstallContext ctx) => _ctx = ctx;

    public static readonly string[] ScannedFolders = { "archive/pc/mod", "r6/scripts", "r6/tweaks", "r6/input", "red4ext", "bin/x64/plugins", "mods" };

    /// <summary>
    /// Walks the game folder and stores the result (a full scan). The part that depends on the selected profile
    /// (identical copies hidden while their mod is on) is derived afterwards, so <see cref="CachedScan"/> can reuse
    /// The walk when only a profile changed.
    /// </summary>
    public ScanResult Scan()
    {
        var raw = ScanFiles();
        _scanCache = (Store.ScanVersion, GameRoot, raw);
        return Finish(raw, SelectedProfile);
    }

    private (long Version, string Root, RawScan Raw)? _scanCache;

    /// <summary>
    /// The last scan, if nothing it depends on changed (packages, ledger, game root) and nothing touched the game
    /// Folder through AFTERLIFE since; otherwise a fresh one. Changes made outside AFTERLIFE are seen on the next
    /// Full <see cref="Scan"/>, which the app runs on start, on a manual refresh and when its window is activated.
    /// </summary>
    public ScanResult CachedScan() =>
        _scanCache is { } c && c.Version == Store.ScanVersion && c.Root == GameRoot ? Finish(c.Raw, SelectedProfile) : Scan();

    /// <summary>Forget the last scan: something changed the game folder (apply, launch, moving files aside).</summary>
    public void InvalidateScan() => _scanCache = null;

    private sealed record RawScan(
        List<(string Path, long Size)> Unmanaged,
        Dictionary<string, List<string>> InTheWay,
        Dictionary<string, List<string>> Same,
        Dictionary<string, List<string>> SamePackages,
        Dictionary<string, string> Runtime,
        HashSet<string> External,
        Dictionary<string, string?> Versions,
        List<string> Archives);

    private RawScan ScanFiles()
    {
        var ledger = Store.GetLedger();
        var unmanaged = new List<(string, long)>();
        var rootFull = Path.GetFullPath(GameRoot);
        foreach (var folder in ScannedFolders)
        {
            var dir = GamePath.Parse(folder).ToOsPath(GameRoot);
            foreach (var file in _fs.EnumerateFiles(dir))
            {
                var rel = Path.GetRelativePath(rootFull, file).Replace(Path.DirectorySeparatorChar, '/');
                if (!GamePath.TryCreate(rel, out var gp, out _)) { unmanaged.Add((rel, 0)); continue; }
                if (ledger.ContainsKey(gp.Key)) continue;
                if (gp.FileName.Contains(".afl-", StringComparison.Ordinal) && gp.FileName.EndsWith(".tmp", StringComparison.Ordinal)) continue;
                if (UnmanagedGrouping.IsGameData(gp.Value)) continue;
                unmanaged.Add((gp.Value, _fs.GetMeta(file)?.Size ?? 0));
            }
        }
        // loose files of manually installed core mods outside the scanned folders, so they can be removed as a whole
        foreach (var (signature, extras) in FrameworkExtras)
        {
            var sig = GamePath.Parse(signature);
            if (ledger.ContainsKey(sig.Key) || !_fs.FileExists(sig.ToOsPath(GameRoot))) continue;
            foreach (var extra in extras)
            {
                var gp = GamePath.Parse(extra);
                var os = gp.ToOsPath(GameRoot);
                if (ledger.ContainsKey(gp.Key) || !_fs.FileExists(os) || unmanaged.Any(u => string.Equals(u.Item1, gp.Value, StringComparison.OrdinalIgnoreCase))) continue;
                unmanaged.Add((gp.Value, _fs.GetMeta(os)?.Size ?? 0));
            }
        }
        // Every path a library package wants to write is checked too, wherever it is (bin/x64, r6/config, engine …).
        // A file there that AFTERLIFE did not write blocks that package (FILE IN THE WAY), so it must be visible and removable.
        // A file with exactly the content the package ships is not in the way: APPLY adopts it.
        var inTheWay = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var same = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var samePackages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var listed = unmanaged.Select(u => u.Item1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hashes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var packages = Store.GetPackages();
        foreach (var pkg in packages)
            foreach (var f in pkg.Files)
            {
                if (!GamePath.TryCreate(f.Target, out var gp, out _) || ledger.ContainsKey(gp.Key) || IsProtectedGameFile(gp.Value)) continue;
                var os = gp.ToOsPath(GameRoot);
                var meta = _fs.GetMeta(os);
                if (meta is null) continue;
                var identical = false;
                if (meta.Size == f.Size)
                {
                    if (!hashes.TryGetValue(gp.Value, out var h))
                    {
                        try { h = _fs.Hash(os); } catch (IOException) { h = null; }
                        hashes[gp.Value] = h;
                    }
                    identical = h == f.Hash;
                }
                var map = identical ? same : inTheWay;
                if (!map.TryGetValue(gp.Value, out var names)) map[gp.Value] = names = new List<string>();
                if (!names.Contains(pkg.ModName)) names.Add(pkg.ModName);
                if (identical)
                {
                    if (!samePackages.TryGetValue(gp.Value, out var ids)) samePackages[gp.Value] = ids = new List<string>();
                    ids.Add(pkg.PackageId);
                }
                if (listed.Add(gp.Value)) unmanaged.Add((gp.Value, meta.Size));
            }
        foreach (var k in inTheWay.Keys) { same.Remove(k); samePackages.Remove(k); } // a differing copy for another mod still blocks
        // logs and settings that mods deployed by AFTERLIFE write into their own folders are theirs, not manual mods
        // (kept while the mod is in the library, also when it is switched off: they are its settings)
        var candidates = unmanaged.Select(u => u.Item1).Where(u => !inTheWay.ContainsKey(u) && !same.ContainsKey(u)).ToList();
        var runtime = RuntimeOwners(candidates, ledger);
        var known = packages.Select(p => p.PackageId).ToHashSet(StringComparer.Ordinal);
        foreach (var (path, owner) in LoadRuntimeOwners())
            if (!runtime.ContainsKey(path) && known.Contains(owner) && candidates.Contains(path, StringComparer.OrdinalIgnoreCase)) runtime[path] = owner;
        Store.SetMeta(RuntimeOwnersKey, Json.Serialize(runtime));
        if (runtime.Count > 0) unmanaged = unmanaged.Where(u => !runtime.ContainsKey(u.Item1)).ToList();
        var external = new HashSet<string>(StringComparer.Ordinal);
        var versions = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var c in FrameworkCatalog.Contracts)
            foreach (var sig in c.SignaturePaths)
            {
                var gp = GamePath.Parse(sig);
                if (ledger.ContainsKey(gp.Key) || !_fs.FileExists(gp.ToOsPath(GameRoot))) continue;
                external.Add(c.Capability);
                versions[c.Capability] = VersionFiles(c.Capability, sig).Select(v => WindowsGameEnvironment.FileVersionOf(GamePath.Parse(v).ToOsPath(GameRoot))).FirstOrDefault(v => v is not null);
            }
        var archives = unmanaged
            .Select(u => GamePath.Parse(u.Item1))
            .Where(p => string.Equals(p.ParentValue, "archive/pc/mod", StringComparison.OrdinalIgnoreCase) && p.Extension == ".archive")
            .Select(p => p.FileName)
            .ToList();
        Store.ReplaceUnmanaged(unmanaged);
        return new RawScan(unmanaged, inTheWay, same, samePackages, runtime, external, versions, archives);
    }

    /// <summary>The profile-dependent part of a scan: identical copies of a mod that is on are hidden from the manual mods.</summary>
    private static ScanResult Finish(RawScan raw, ProfileState selected)
    {
        var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, ids) in raw.SamePackages)
            if (ids.Any(selected.IsEnabled)) hidden.Add(path);
        var blockers = raw.InTheWay.ToDictionary(k => k.Key, k => (IReadOnlyList<string>)k.Value, StringComparer.OrdinalIgnoreCase);
        return new ScanResult(raw.Unmanaged, raw.External, raw.Archives)
        {
            ExternalVersions = raw.Versions,
            Detected = UnmanagedGrouping.WithBlockers(UnmanagedGrouping.Group(raw.Unmanaged), blockers,
                raw.Same.ToDictionary(k => k.Key, k => (IReadOnlyList<string>)k.Value, StringComparer.OrdinalIgnoreCase), hidden),
            InTheWay = blockers,
            RuntimeFiles = raw.Runtime,
        };
    }

    /// <summary>Folders many mods share: a file directly in them belongs to nobody in particular.</summary>
    private static readonly HashSet<string> SharedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "archive", "archive/pc", "archive/pc/mod", "bin", "bin/x64", "bin/x64/plugins", "bin/x64/plugins/cyber_engine_tweaks/mods",
        "r6", "r6/scripts", "r6/tweaks", "r6/config", "r6/config/redsUserHints", "r6/config/settings", "r6/input", "r6/cache", "r6/cache/modded",
        "r6/audioware", "r6/logs", "red4ext", "red4ext/plugins", "red4ext/logs", "mods", "engine", "engine/config", "engine/config/platform",
        "engine/config/platform/pc", "engine/tools",
    };

    /// <summary>
    /// Which deployed package a file AFTERLIFE did not write belongs to: the nearest own (not shared) folder of
    /// One deployed package, or a runtime folder of a core mod deployed by AFTERLIFE.
    /// </summary>
    private Dictionary<string, string> RuntimeOwners(IEnumerable<string> paths, Dictionary<string, LedgerEntry> ledger)
    {
        var subtree = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var direct = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in ledger.Values)
        {
            if (e.Owner == "afterlife") continue;
            var dir = ParentOf(e.Path);
            if (!direct.TryGetValue(dir, out var d)) direct[dir] = d = new HashSet<string>(StringComparer.Ordinal);
            d.Add(e.Owner);
            for (var x = dir; !SharedFolders.Contains(x); x = ParentOf(x))
            {
                if (!subtree.TryGetValue(x, out var t)) subtree[x] = t = new HashSet<string>(StringComparer.Ordinal);
                t.Add(e.Owner);
            }
        }
        var runtimeFolders = new List<(string Folder, IReadOnlyList<string> Exclude, string Owner)>();
        var runtimeFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var owners = ledger.Values.Select(l => l.Owner).Distinct().ToList();
        var library = Store.GetPackages();
        foreach (var c in FrameworkCatalog.Contracts.Where(c => c.RuntimeFolders.Count > 0 || c.RuntimeFiles.Count > 0))
        {
            // the deployed copy first; else a copy in the library (its files stay its settings)
            var owner = owners.Select(Store.GetPackage).FirstOrDefault(p => p is not null && p.Provides.Contains(c.Capability))
                        ?? library.FirstOrDefault(p => p.Provides.Contains(c.Capability));
            if (owner is null) continue;
            foreach (var f in c.RuntimeFolders) runtimeFolders.Add((f, c.RuntimeExclude, owner.PackageId));
            foreach (var f in c.RuntimeFiles) runtimeFiles[GamePath.Parse(f).Value] = owner.PackageId; // e.g. red4ext/config.ini
        }
        // own folders of every mod in the library (not only the deployed ones): a lock file or cache a CET mod
        // wrote into its folder belongs to it even when it is switched off right now
        var libSubtree = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var modIdOf = library.ToDictionary(p => p.PackageId, p => p.ModId, StringComparer.Ordinal);
        foreach (var p in library)
            foreach (var f in p.Files)
            {
                if (!GamePath.TryCreate(f.Target, out var gp, out _)) continue;
                for (var x = ParentOf(gp.Value); !SharedFolders.Contains(x); x = ParentOf(x))
                {
                    if (!libSubtree.TryGetValue(x, out var t)) libSubtree[x] = t = new HashSet<string>(StringComparer.Ordinal);
                    t.Add(p.PackageId);
                }
            }
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            string? owner = null;
            for (var x = ParentOf(path); !SharedFolders.Contains(x); x = ParentOf(x))
            {
                if (!subtree.TryGetValue(x, out var t)) continue;
                if (t.Count == 1) owner = t.First();
                else if (direct.TryGetValue(x, out var d) && d.Count == 1) owner = d.First();
                break;
            }
            owner ??= runtimeFiles.TryGetValue(path, out var rf) ? rf : null;
            owner ??= runtimeFolders.FirstOrDefault(r => path.StartsWith(r.Folder + "/", StringComparison.OrdinalIgnoreCase)
                && !r.Exclude.Any(e => path.StartsWith(e + "/", StringComparison.OrdinalIgnoreCase))).Owner;
            if (owner is null)
                for (var x = ParentOf(path); !SharedFolders.Contains(x); x = ParentOf(x))
                {
                    if (!libSubtree.TryGetValue(x, out var t)) continue;
                    // one mod, or several versions of the same mod
                    if (t.Select(id => modIdOf[id]).Distinct(StringComparer.Ordinal).Count() == 1) owner = t.First();
                    break;
                }
            if (owner is not null) result[path] = owner;
        }
        return result;
    }

    private static string ParentOf(string path)
    {
        var i = path.LastIndexOf('/');
        return i < 0 ? "" : path[..i];
    }

    private const string RuntimeOwnersKey = "runtime.owners";

    private Dictionary<string, string> LoadRuntimeOwners()
    {
        var json = Store.GetMeta(RuntimeOwnersKey);
        if (json is null) return new(StringComparer.OrdinalIgnoreCase);
        try { return new Dictionary<string, string>(Json.Deserialize<Dictionary<string, string>>(json) ?? [], StringComparer.OrdinalIgnoreCase); }
        catch (System.Text.Json.JsonException) { return new(StringComparer.OrdinalIgnoreCase); }
    }

    /// <summary>When a package is deleted from the library, the files it created itself go to Removed as "Leftovers of X" (can be restored).</summary>
    internal List<RemovalResult> MoveRuntimeLeftovers(IReadOnlyCollection<string> deletedPackages, IReadOnlyDictionary<string, string> names)
    {
        var results = new List<RemovalResult>();
        var map = LoadRuntimeOwners();
        if (map.Count == 0 || deletedPackages.Count == 0) return results;
        var ledger = Store.GetLedger();
        foreach (var group in map.Where(r => deletedPackages.Contains(r.Value)).GroupBy(r => r.Value))
        {
            var files = group.Select(g => GamePath.Parse(g.Key))
                .Where(gp => !ledger.ContainsKey(gp.Key) && _fs.FileExists(gp.ToOsPath(GameRoot))).ToList();
            if (files.Count == 0) continue;
            var name = names.TryGetValue(group.Key, out var n) ? n : group.Key;
            try { results.Add(Removed.Remove(GameRoot, files, $"Leftovers of {name}")); }
            catch (IOException) { } // a log still open: it stays and shows in Scan manual mods
        }
        return results;
    }

    /// <summary>Base game files AFTERLIFE never moves, even when a mod ships a file with the same path.</summary>
    public static bool IsProtectedGameFile(string path) =>
        string.Equals(path, "bin/x64/Cyberpunk2077.exe", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("archive/pc/content/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("archive/pc/ep1/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Files in the game at this package's paths that AFTERLIFE did not write.</summary>
    public IReadOnlyList<string> ForeignFilesOf(string packageId)
    {
        var pkg = Store.GetPackage(packageId) ?? throw new InvalidOperationException($"unknown package {packageId}");
        var ledger = Store.GetLedger();
        return pkg.Files.Select(f => GamePath.Parse(f.Target))
            .Where(g => !ledger.ContainsKey(g.Key) && _fs.FileExists(g.ToOsPath(GameRoot)))
            .Select(g => g.Value).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// REPLACE: moves the files that block a package out of the game into the library ("removed", restorable),
    /// So the next apply can install the package. Protected base game files are refused.
    /// </summary>
    public RemovalResult MoveAsideFor(string packageId)
    {
        var pkg = Store.GetPackage(packageId) ?? throw new InvalidOperationException($"unknown package {packageId}");
        var files = ForeignFilesOf(packageId);
        if (files.Count == 0) return new RemovalResult(null, 0, []);
        var prot = files.Where(IsProtectedGameFile).ToList();
        if (prot.Count > 0) throw new InvalidOperationException($"{prot[0]} is a base game file; AFTERLIFE does not move it");
        return RemoveUnmanaged(files, $"Replaced by {pkg.ModName}");
    }

    /// <summary>Signature file → loose files that belong to the same core mod (only collected when the signature exists).</summary>
    public static readonly (string Signature, string[] Extras)[] FrameworkExtras =
    {
        ("bin/x64/plugins/cyber_engine_tweaks.asi", new[] { "bin/x64/version.dll", "bin/x64/global.ini" }),
        ("red4ext/RED4ext.dll", new[] { "bin/x64/winmm.dll" }),
        ("engine/tools/scc.exe", new[] { "engine/tools/scc.exe", "engine/tools/scc_lib.dll", "engine/config/base/scripts.ini", "r6/config/cybercmd/scc.toml" }),
    };

    public static bool IsRemovablePath(string path) =>
        ScannedFolders.Any(f => path.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase))
        || FrameworkExtras.Any(x => x.Extras.Any(e => string.Equals(e, path, StringComparison.OrdinalIgnoreCase)));

    private static IEnumerable<string> VersionFiles(string capability, string signature) => capability switch
    {
        Capabilities.Redscript => new[] { "engine/tools/scc_lib.dll", signature },
        _ => new[] { signature },
    };

    /// <summary>
    /// Moves files AFTERLIFE did not install out of the game into the library's "removed" folder (reversible).
    /// Refuses managed files, files outside the mod folders and anything while the game runs or an operation is unfinished.
    /// Core mods can be removed too; the window warns about mods that need them first.
    /// </summary>
    public RemovalResult RemoveUnmanaged(IReadOnlyCollection<string> paths, string label)
    {
        using var _ = InstallLock.Acquire(Library, GameRoot);
        _ctx.EnsureQuiet();
        var ledger = Store.GetLedger();
        var found = Scan().Unmanaged.Select(u => GamePath.Parse(u.Path).Key).ToHashSet(StringComparer.Ordinal);
        var list = new List<GamePath>();
        foreach (var raw in paths)
        {
            var gp = GamePath.Parse(raw);
            if (ledger.ContainsKey(gp.Key)) throw new InvalidOperationException($"{gp.Value} is managed by AFTERLIFE; switch the mod off instead");
            if (IsProtectedGameFile(gp.Value)) throw new InvalidOperationException($"{gp.Value} is a base game file; AFTERLIFE does not move it");
            // in a mod folder, or found by the scan (loose core-mod files, files in the way of a library mod)
            if (!found.Contains(gp.Key) && !ScannedFolders.Any(f => gp.Value.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"{gp.Value} is outside the mod folders and was not found by the scan");
            list.Add(gp);
        }
        var r = Removed.Remove(GameRoot, list, label);
        ActivityLog.Write("removed", $"'{label}': moved {r.Moved} file(s) aside (batch {r.BatchId ?? "-"}, {r.Skipped.Count} skipped)\n" + string.Join("\n", list.Select(g => g.Value)));
        Scan();
        return r;
    }

    public RemovalResult RestoreRemoved(string batchId)
    {
        using var _ = InstallLock.Acquire(Library, GameRoot);
        _ctx.EnsureQuiet();
        var r = Removed.Restore(GameRoot, batchId);
        ActivityLog.Write("removed", $"restored batch {batchId}: {r.Moved} file(s) back, {r.Skipped.Count} skipped");
        Scan();
        return r;
    }

    /// <summary>
    /// After a framework was switched off: files it created itself while the game ran (logs, settings, caches)
    /// Are moved into the library ("removed", restorable). Only when the framework is really gone from the game,
    /// Never inside folders of other mods (CET mods), never files AFTERLIFE manages.
    /// </summary>
    public RemovalResult? CleanLeftovers(string capability)
    {
        var c = FrameworkCatalog.Find(capability);
        if (c is null || c.RuntimeFolders.Count == 0) return null;
        using var lockHandle = InstallLock.Acquire(Library, GameRoot);
        _ctx.EnsureQuiet();
        var ledger = Store.GetLedger();
        if (c.SignaturePaths.Any(sig => _fs.FileExists(GamePath.Parse(sig).ToOsPath(GameRoot)))) return null; // still installed
        var rootFull = Path.GetFullPath(GameRoot);
        var list = new List<GamePath>();
        foreach (var folder in c.RuntimeFolders)
            foreach (var file in _fs.EnumerateFiles(GamePath.Parse(folder).ToOsPath(GameRoot)))
            {
                var rel = Path.GetRelativePath(rootFull, file).Replace(Path.DirectorySeparatorChar, '/');
                if (!GamePath.TryCreate(rel, out var gp, out _) || ledger.ContainsKey(gp.Key)) continue;
                if (c.RuntimeExclude.Any(x => gp.Value.StartsWith(x + "/", StringComparison.OrdinalIgnoreCase))) continue;
                list.Add(gp);
            }
        if (list.Count == 0) return null;
        var r = Removed.Remove(GameRoot, list, $"Leftovers of {Capabilities.DisplayName(capability)}");
        ActivityLog.Write("removed", $"leftovers of {Capabilities.DisplayName(capability)}: moved {r.Moved} file(s) aside (batch {r.BatchId ?? "-"})\n" + string.Join("\n", list.Select(g => g.Value)));
        Scan();
        return r;
    }
}
