using System.Diagnostics;
using Afterlife.Core;

namespace Afterlife.Infrastructure;

public sealed record GameFingerprint(string FileVersion, long Size, long MTimeTicks, string? GameVersion = null)
{
    public string Display => GameVersion is null ? $"build {FileVersion}" : $"{GameVersion} · build {FileVersion}";

    /// <summary>The same executable: size and timestamp match (the version text format may differ between AFTERLIFE versions).</summary>
    public bool SameExecutable(GameFingerprint other) => Size == other.Size && MTimeTicks == other.MTimeTicks;
}

public static class VersionText
{
    /// <summary>"2.50" → "2.5", "2.31" → "2.31", "2.3" → "2.3"; null when it does not look like a game patch number.</summary>
    public static string? GamePatch(string? productVersion)
    {
        if (string.IsNullOrWhiteSpace(productVersion)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(productVersion.Trim(), @"^(\d)\.(\d{1,2})(?:$|[^\d.])");
        if (!m.Success) return null;
        var minor = m.Groups[2].Value;
        if (minor.Length == 2 && minor[1] == '0') minor = minor[..1];
        return $"{m.Groups[1].Value}.{minor}";
    }

    /// <summary>"1.37.1.0" → "1.37.1", "v1.31.0+abc" → "1.31.0"; null for empty or "0.0.0.0".</summary>
    public static string? Clean(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        var v = version.Trim().TrimStart('v', 'V');
        var cut = v.IndexOfAny(['+', ' ', '-']);
        if (cut > 0) v = v[..cut];
        var parts = v.Split('.').ToList();
        while (parts.Count > 3 && parts[^1] == "0") parts.RemoveAt(parts.Count - 1);
        v = string.Join('.', parts);
        return v.Length == 0 || v.All(c => c == '0' || c == '.') ? null : v;
    }
}

/// <summary>Port for things outside AFTERLIFE's control: the running game and its executable.</summary>
public interface IGameEnvironment
{
    bool IsGameRunning();
    GameFingerprint? Fingerprint(string gameRoot);

    /// <summary>Runs a game tool (REDmod) and waits for it. Returns the exit code, or null when it timed out.</summary>
    int? RunTool(string exe, IReadOnlyList<string> args, string workDir, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = workDir, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"could not start {Path.GetFileName(exe)}");
        if (!p.WaitForExit(timeout)) { try { p.Kill(true); } catch (InvalidOperationException) { } return null; }
        return p.ExitCode;
    }
}

public enum RedModOutcome { NoRedMods, UpToDate, Deployed, ToolMissing, Failed }

public sealed record RedModResult(RedModOutcome Outcome, IReadOnlyList<string> Mods, string Message)
{
    public bool LaunchModded => Outcome is RedModOutcome.UpToDate or RedModOutcome.Deployed;
}

public sealed class WindowsGameEnvironment : IGameEnvironment
{
    public const string ExeRelativePath = "bin/x64/Cyberpunk2077.exe";

    public bool IsGameRunning()
    {
        try { return Process.GetProcessesByName("Cyberpunk2077").Length > 0; }
        catch { return false; }
    }

    public GameFingerprint? Fingerprint(string gameRoot)
    {
        var exe = GamePath.Parse(ExeRelativePath).ToOsPath(gameRoot);
        var fi = new FileInfo(exe);
        if (!fi.Exists) return null;
        string version = "unknown";
        string? patch = null;
        try
        {
            var v = FileVersionInfo.GetVersionInfo(exe);
            // The numeric parts are reliable (3.0.88.46235); the text resource may be packed differently (3.0.5813403).
            version = v.FileMajorPart + v.FileMinorPart + v.FileBuildPart + v.FilePrivatePart > 0
                ? $"{v.FileMajorPart}.{v.FileMinorPart}.{v.FileBuildPart}.{v.FilePrivatePart}"
                : v.FileVersion ?? "unknown";
            patch = VersionText.GamePatch(v.ProductVersion)
                ?? (v.ProductMajorPart is > 0 and < 3 ? VersionText.GamePatch($"{v.ProductMajorPart}.{v.ProductMinorPart:00}") : null);
        }
        catch { /* keep unknown */ }
        return new GameFingerprint(version, fi.Length, fi.LastWriteTimeUtc.Ticks, patch);
    }

    /// <summary>Version of a framework installed outside AFTERLIFE, read from its own binary.</summary>
    public static string? FileVersionOf(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var v = FileVersionInfo.GetVersionInfo(path);
            return VersionText.Clean(v.ProductVersion) ?? VersionText.Clean(v.FileVersion);
        }
        catch { return null; }
    }
}

public sealed class InstallBusyException(string message) : Exception(message);

/// <summary>Exclusive lock per game install, shared by every AFTERLIFE process (file lock in the library).</summary>
public sealed class InstallLock : IDisposable
{
    private readonly FileStream _stream;
    private InstallLock(FileStream s) => _stream = s;

    public static InstallLock Acquire(LibraryPaths lib, string gameRoot)
    {
        Directory.CreateDirectory(lib.Locks);
        var id = ContentHash.Of(CanonicalRoot(gameRoot))[..16];
        var path = Path.Combine(lib.Locks, id + ".lock");
        try { return new InstallLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
        catch (IOException) { throw new InstallBusyException("another AFTERLIFE window or command is changing this game install"); }
    }

    public static string CanonicalRoot(string gameRoot)
    {
        var full = Path.GetFullPath(gameRoot).TrimEnd(Path.DirectorySeparatorChar);
        try
        {
            var target = new DirectoryInfo(full).ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null) full = target.FullName.TrimEnd(Path.DirectorySeparatorChar);
        }
        catch { /* keep full path */ }
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }

    public void Dispose() => _stream.Dispose();
}

public sealed record DeleteOutcome(IReadOnlyList<string> Deleted, IReadOnlyList<(string Name, string Reason)> Kept, ExecutionResult? Execution)
{
    /// <summary>P55: files the deleted mods had created themselves, moved to Removed.</summary>
    public int Leftovers { get; init; }
}

public sealed record Dependent(string PackageId, string Name, IReadOnlyList<string> Needs, IReadOnlyList<string> Profiles, bool InGame);

public sealed record ScanResult(IReadOnlyList<(string Path, long Size)> Unmanaged, IReadOnlySet<string> ExternalCapabilities, IReadOnlyList<string> UnmanagedArchiveNames)
{
    /// <summary>Version read from the binary of each framework installed outside AFTERLIFE (null when it has none).</summary>
    public IReadOnlyDictionary<string, string?> ExternalVersions { get; init; } = new Dictionary<string, string?>();

    /// <summary>P32: unmanaged file → library mods that want to write the same path.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> InTheWay { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    /// <summary>P55: files a mod installed by AFTERLIFE created by itself (logs, settings, caches): path → package id. Not listed as manual mods.</summary>
    public IReadOnlyDictionary<string, string> RuntimeFiles { get; init; } = new Dictionary<string, string>();

    /// <summary>Unmanaged files grouped into likely mods (P6).</summary>
    public IReadOnlyList<DetectedMod> Detected { get; init; } = [];
}

public sealed record PreviewResult(DeploymentSnapshot Snapshot, Plan Plan, ScanResult Scan, IReadOnlyList<Issue> EnvironmentIssues)
{
    public bool CanApply => Plan.CanApply && EnvironmentIssues.All(i => i.Severity != Severity.Blocking);
}

public sealed record ApplyResult(PreviewResult? Preview, ExecutionResult? Execution, string Message)
{
    public bool Succeeded => Execution?.Status == OperationStatus.Verified || (Preview is not null && Preview.Plan.IsNoop && Preview.CanApply && Execution is null);
}

public enum RecoveryChoice { Finish, ReturnToPreviousGeneration }

public sealed record StatusReport(
    string GameRoot,
    ProfileState Selected,
    GenerationRecord? LastGeneration,
    OperationRecord? Unfinished,
    GameFingerprint? Fingerprint,
    bool BuildChanged,
    int LedgerFiles,
    int Packages);

/// <summary>
/// Application service that owns one game install: scanning, planning, applying, recovery and profile edits.
/// Every mutation of the game folder goes through here, under the install lock.
/// </summary>
public sealed class InstallCoordinator : IDisposable
{
    public static readonly string[] ScannedFolders = { "archive/pc/mod", "r6/scripts", "r6/tweaks", "r6/input", "red4ext", "bin/x64/plugins", "mods" };

    private readonly IFileOps _fs;
    private readonly IGameEnvironment _env;

    public LibraryPaths Library { get; }
    public AfterlifeStore Store { get; }
    public PackageLibrary Packages { get; }
    public string GameRoot { get; private set; }
    public Quarantine Removed { get; }

    private InstallCoordinator(LibraryPaths lib, AfterlifeStore store, IFileOps fs, IGameEnvironment env)
    {
        Library = lib;
        Store = store;
        _fs = fs;
        _env = env;
        GameRoot = store.GetMeta("game.root") ?? throw new InvalidOperationException("library is not initialized");
        Packages = new PackageLibrary(lib, store);
        Removed = new Quarantine(lib, fs);
    }

    public static InstallCoordinator Initialize(string gameRoot, string libraryRoot, IFileOps? fs = null, IGameEnvironment? env = null)
    {
        var lib = new LibraryPaths(libraryRoot);
        var full = Path.GetFullPath(gameRoot);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException($"game folder not found: {full}");
        var libFull = lib.Root;
        if (libFull.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(libFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(full, libFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("the library must not overlap with the game folder");
        lib.EnsureCreated();
        var store = AfterlifeStore.Open(lib.Database);
        var existing = store.GetMeta("game.root");
        if (existing is not null && !string.Equals(existing, full, StringComparison.OrdinalIgnoreCase))
        {
            store.Dispose();
            throw new InvalidOperationException($"library already belongs to {existing}");
        }
        store.SetMeta("game.root", full);
        if (store.GetProfile(ProfileState.ModsOffId) is null) store.SaveProfile(ProfileState.CreateModsOff());
        if (store.GetProfiles().All(p => p.IsSystemModsOff))
        {
            var street = ProfileState.Create("street", "Street");
            store.SaveProfile(street);
            store.SetMeta("profile.selected", street.Id);
        }
        var c = new InstallCoordinator(lib, store, fs ?? new PhysicalFileOps(), env ?? new WindowsGameEnvironment());
        var fp = c._env.Fingerprint(full);
        if (fp is not null && store.GetMeta("game.fingerprint.ack") is null) store.SetMeta("game.fingerprint.ack", Json.Serialize(fp));
        return c;
    }

    public static InstallCoordinator Open(string libraryRoot, IFileOps? fs = null, IGameEnvironment? env = null)
    {
        var lib = new LibraryPaths(libraryRoot);
        if (!File.Exists(lib.Database)) throw new FileNotFoundException("no AFTERLIFE library here; run init first", lib.Database);
        return new InstallCoordinator(lib, AfterlifeStore.Open(lib.Database), fs ?? new PhysicalFileOps(), env ?? new WindowsGameEnvironment());
    }

    // ---------------- profiles ----------------
    public IReadOnlyList<ProfileState> Profiles => Store.GetProfiles();

    public ProfileState SelectedProfile =>
        Store.GetProfile(Store.GetMeta("profile.selected") ?? ProfileState.ModsOffId) ?? Store.GetProfile(ProfileState.ModsOffId)!;

    public void SelectProfile(string profileId)
    {
        if (Store.GetProfile(profileId) is null) throw new InvalidOperationException($"unknown profile {profileId}");
        Store.SetMeta("profile.selected", profileId);
    }

    public ProfileState CreateProfile(string name, string? copyFromId = null)
    {
        var id = "p-" + Guid.NewGuid().ToString("N")[..8];
        var baseProfile = copyFromId is null ? null : Store.GetProfile(copyFromId);
        var p = baseProfile is null || baseProfile.IsSystemModsOff
            ? ProfileState.Create(id, name)
            : baseProfile with { Id = id, Name = name, Revision = 1, IsSystemModsOff = false };
        Store.SaveProfile(p);
        return p;
    }

    /// <summary>P76: shown name of a mod in AFTERLIFE; files in the game do not change. Empty = back to the original name.</summary>
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
    /// P72: deletes a profile. Mods off cannot be deleted, nor the profile that is in the game now (switch the game
    /// to another profile first, so nothing in the game belongs to a missing profile). Mods stay in the library;
    /// a collection that used the profile keeps its record.
    /// </summary>
    public void DeleteProfile(string profileId)
    {
        var p = Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        if (p.IsSystemModsOff) throw new InvalidOperationException("Mods off cannot be deleted");
        if (Store.GetLastGeneration()?.ProfileId == profileId && Store.GetLedger().Count > 0)
            throw new InvalidOperationException($"{p.Name} is in the game now: select another profile and apply first");
        if (SelectedProfile.Id == profileId)
        {
            var next = Store.GetLastGeneration()?.ProfileId is { } d && d != profileId && Store.GetProfile(d) is not null ? d : ProfileState.ModsOffId;
            Store.SetMeta("profile.selected", next);
        }
        foreach (var r in CollectionStore.List(Store).Where(r => r.ProfileId == profileId))
            CollectionStore.Save(Store, r with { ProfileId = null });
        Store.DeleteProfile(profileId);
    }

    public ProfileState UpdateSelected(Func<ProfileState, ProfileState> change)
    {
        var p = SelectedProfile;
        if (p.IsSystemModsOff)
            throw new InvalidOperationException("Mods off cannot be edited; select or create another profile");
        var updated = change(p);
        Store.SaveProfile(updated);
        return updated;
    }

    public ProfileState SetEnabled(string packageId, bool enabled)
    {
        if (Store.GetPackage(packageId) is null) throw new InvalidOperationException($"unknown package {packageId}");
        return UpdateSelected(p => p.WithEntry(packageId, enabled));
    }

    // ---------------- library ----------------
    public ImportResult Import(ImportRequest request) => Packages.Import(request);

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
    /// because the files on disk are now exactly the layer's content.
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

    // ---------------- inventory ----------------
    public ScanResult Scan()
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
                unmanaged.Add((gp.Value, _fs.GetMeta(file)?.Size ?? 0));
            }
        }
        // P15: loose files of manually installed core mods outside the scanned folders, so they can be removed as a whole.
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
        // P32: every path a library package wants to write is checked too, wherever it is (bin/x64, r6/config, engine …).
        // A file there that AFTERLIFE did not write blocks that package (FILE IN THE WAY), so it must be visible and removable.
        // P45: a file with exactly the content the package ships is not in the way: APPLY adopts it (P31).
        var inTheWay = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var same = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var listed = unmanaged.Select(u => u.Item1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = SelectedProfile;
        var hashes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pkg in Store.GetPackages())
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
                if (identical && selected.IsEnabled(pkg.PackageId)) hidden.Add(gp.Value);
                if (listed.Add(gp.Value)) unmanaged.Add((gp.Value, meta.Size));
            }
        foreach (var k in inTheWay.Keys) { same.Remove(k); hidden.Remove(k); } // a differing copy for another mod still blocks
        // P55: logs and settings that mods deployed by AFTERLIFE write into their own folders are theirs, not manual mods
        // (kept while the mod is in the library, also when it is switched off: they are its settings; P55)
        var candidates = unmanaged.Select(u => u.Item1).Where(u => !inTheWay.ContainsKey(u) && !same.ContainsKey(u)).ToList();
        var runtime = RuntimeOwners(candidates, ledger);
        var known = Store.GetPackages().Select(p => p.PackageId).ToHashSet(StringComparer.Ordinal);
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
        var blockers = inTheWay.ToDictionary(k => k.Key, k => (IReadOnlyList<string>)k.Value, StringComparer.OrdinalIgnoreCase);
        return new ScanResult(unmanaged, external, archives)
        {
            ExternalVersions = versions,
            Detected = UnmanagedGrouping.WithBlockers(UnmanagedGrouping.Group(unmanaged), blockers,
                same.ToDictionary(k => k.Key, k => (IReadOnlyList<string>)k.Value, StringComparer.OrdinalIgnoreCase), hidden),
            InTheWay = blockers,
            RuntimeFiles = runtime,
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
    /// P55: which deployed package a file AFTERLIFE did not write belongs to: the nearest own (not shared) folder of
    /// one deployed package, or a runtime folder of a core mod deployed by AFTERLIFE (P37).
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
        var owners = ledger.Values.Select(l => l.Owner).Distinct().ToList();
        foreach (var c in FrameworkCatalog.Contracts.Where(c => c.RuntimeFolders.Count > 0))
        {
            var owner = owners.Select(Store.GetPackage).FirstOrDefault(p => p is not null && p.Provides.Contains(c.Capability));
            if (owner is null) continue;
            foreach (var f in c.RuntimeFolders) runtimeFolders.Add((f, c.RuntimeExclude, owner.PackageId));
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
            owner ??= runtimeFolders.FirstOrDefault(r => path.StartsWith(r.Folder + "/", StringComparison.OrdinalIgnoreCase)
                && !r.Exclude.Any(e => path.StartsWith(e + "/", StringComparison.OrdinalIgnoreCase))).Owner;
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

    /// <summary>P55: when a package is deleted from the library, the files it created itself go to Removed as "Leftovers of X" (can be restored).</summary>
    private List<RemovalResult> MoveRuntimeLeftovers(IReadOnlyCollection<string> deletedPackages, IReadOnlyDictionary<string, string> names)
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

    /// <summary>Files in the game at this package's paths that AFTERLIFE did not write (P30/P32).</summary>
    public IReadOnlyList<string> ForeignFilesOf(string packageId)
    {
        var pkg = Store.GetPackage(packageId) ?? throw new InvalidOperationException($"unknown package {packageId}");
        var ledger = Store.GetLedger();
        return pkg.Files.Select(f => GamePath.Parse(f.Target))
            .Where(g => !ledger.ContainsKey(g.Key) && _fs.FileExists(g.ToOsPath(GameRoot)))
            .Select(g => g.Value).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// P30 REPLACE: moves the files that block a package out of the game into the library ("removed", restorable),
    /// so the next apply can install the package. Protected base game files are refused.
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

    // ---------------- unmanaged files (P6) ----------------
    /// <summary>
    /// Moves files AFTERLIFE did not install out of the game into the library's "removed" folder (reversible).
    /// Refuses managed files, files outside the mod folders and anything while the game runs or an operation is unfinished.
    /// Core mods can be removed too (P15); the window warns about mods that need them first.
    /// </summary>
    public RemovalResult RemoveUnmanaged(IReadOnlyCollection<string> paths, string label)
    {
        using var _ = InstallLock.Acquire(Library, GameRoot);
        EnsureQuiet();
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
        Scan();
        return r;
    }

    public RemovalResult RestoreRemoved(string batchId)
    {
        using var _ = InstallLock.Acquire(Library, GameRoot);
        EnsureQuiet();
        var r = Removed.Restore(GameRoot, batchId);
        Scan();
        return r;
    }

    private void EnsureQuiet()
    {
        if (_env.IsGameRunning()) throw new InvalidOperationException("Cyberpunk 2077 is running; close it first");
        if (Store.GetUnfinishedOperation() is not null) throw new InvalidOperationException("an operation did not finish; run recovery first");
    }

    // ---------------- game folder (P7) ----------------
    /// <summary>
    /// Points the library at another game folder. Allowed when nothing is deployed (apply Mods off first),
    /// or when the same install was moved and every managed file is found unchanged at the new place.
    /// </summary>
    public void ChangeGameRoot(string newRoot)
    {
        var full = Path.GetFullPath(newRoot);
        if (string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), GameRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return;
        if (!File.Exists(GamePath.Parse(WindowsGameEnvironment.ExeRelativePath).ToOsPath(full)))
            throw new InvalidOperationException("Cyberpunk2077.exe not found in bin\\x64 of that folder");
        var libFull = Library.Root;
        if (libFull.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(full, libFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("the library must not be inside the game folder");
        using var _ = InstallLock.Acquire(Library, GameRoot);
        if (Store.GetUnfinishedOperation() is not null) throw new InvalidOperationException("an operation did not finish; run recovery first");
        if (_env.IsGameRunning()) throw new InvalidOperationException("Cyberpunk 2077 is running; close it first");
        var ledger = Store.GetLedger();
        var moved = ledger.Count > 0 && ledger.Values.All(l =>
        {
            var os = GamePath.Parse(l.Path).ToOsPath(full);
            return _fs.FileExists(os) && _fs.Hash(os) == l.Hash;
        });
        if (ledger.Count > 0 && !moved)
            throw new InvalidOperationException("mods are deployed in the current folder; select Mods off and apply first, then change the folder");
        if (Store.GetBaseline(ModlistFormat.RelativePath) is not null && !moved)
            throw new InvalidOperationException("AFTERLIFE still keeps modlist.txt of the current folder; select Mods off and apply first");
        Store.SetMeta("game.root", full);
        GameRoot = full;
        var fp = _env.Fingerprint(full);
        if (fp is not null && !moved) Store.SetMeta("game.fingerprint.ack", Json.Serialize(fp));
        Scan();
    }

    // ---------------- bulk profile edits (P11) ----------------
    public ProfileState SetEnabledMany(string profileId, IReadOnlyCollection<string> packageIds, bool enabled)
    {
        var p = Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        if (p.IsSystemModsOff) throw new InvalidOperationException("Mods off cannot be edited; select or create another profile");
        foreach (var id in packageIds)
        {
            if (Store.GetPackage(id) is null) throw new InvalidOperationException($"unknown package {id}");
            p = p.WithEntry(id, enabled);
        }
        Store.SaveProfile(p);
        return p;
    }

    /// <summary>
    /// Switches packages off in every profile and deletes those that are no longer needed. Packages whose files are
    /// still in the game are kept and reported (the window uses <see cref="DeleteEverywhere"/> instead).
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
    /// P21: one confirmed action. Switches the packages off in every profile, takes their files out of the game
    /// (re-deploying exactly what is in the game now minus these packages, so other pending edits stay pending)
    /// and deletes them from the library.
    /// </summary>
    public DeleteOutcome DeleteEverywhere(IReadOnlyCollection<string> packageIds, IProgress<DeployProgress>? progress = null)
    {
        using var _ = InstallLock.Acquire(Library, GameRoot);
        EnsureQuiet();
        var ids = packageIds.ToHashSet();
        var names = ids.ToDictionary(id => id, id => Store.GetPackage(id)?.ModName ?? id);
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
            var scan = Scan();
            var preview = MakePreview(BuildSnapshot(target, scan), scan, null);
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
                scan = Scan();
                preview = MakePreview(BuildSnapshot(target, scan), scan, null);
            }
            if (!preview.CanApply)
                return new DeleteOutcome([], ids.Select(id => (names[id], "could not be taken out of the game: " +
                    string.Join("; ", preview.Plan.Blockers.Concat(preview.EnvironmentIssues.Where(i => i.Severity == Severity.Blocking)).Select(i => i.Message)))).ToList(), null);
            if (!preview.Plan.IsNoop)
            {
                exec = Run(preview.Snapshot, preview.Plan, progress);
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
        var leftovers = MoveRuntimeLeftovers(gone, names);
        return new DeleteOutcome(deleted, kept, exec) { Leftovers = leftovers.Sum(l => l.Moved) };
    }

    /// <summary>P15: managed mods that need one of these capabilities and are wanted somewhere or in the game.</summary>
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

    // ---------------- requirements (P40) ----------------
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

    /// <summary>P41/P43: links a library package to a mod on Nexus (id typed by the user or taken from a link).</summary>
    public PackageManifest LinkToNexus(string packageId, string nexusModId)
    {
        var p = Store.GetPackage(packageId) ?? throw new InvalidOperationException($"unknown package {packageId}");
        if (nexusModId.Length == 0 || !nexusModId.All(char.IsDigit)) throw new InvalidOperationException("a Nexus mod id is a number, for example 107");
        var q = p with { NexusModId = nexusModId, Origin = p.Origin == PackageOrigin.Captured ? p.Origin : PackageOrigin.Nexus };
        Store.UpdatePackage(q);
        return q;
    }

    // ---------------- framework leftovers (P37) ----------------
    /// <summary>
    /// After a framework was switched off: files it created itself while the game ran (logs, settings, caches)
    /// are moved into the library ("removed", restorable). Only when the framework is really gone from the game,
    /// never inside folders of other mods (CET mods), never files AFTERLIFE manages.
    /// </summary>
    public RemovalResult? CleanLeftovers(string capability)
    {
        var c = FrameworkCatalog.Find(capability);
        if (c is null || c.RuntimeFolders.Count == 0) return null;
        using var lockHandle = InstallLock.Acquire(Library, GameRoot);
        EnsureQuiet();
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
        Scan();
        return r;
    }

    // ---------------- collections (P51) ----------------
    public IReadOnlyList<CollectionRecord> Collections() => CollectionStore.List(Store);

    /// <summary>Reads collection.json from a downloaded collection bundle and merges it with the API revision.</summary>
    public CollectionRevisionInfo ReadCollectionBundle(CollectionRevisionInfo api, string bundleFile)
    {
        var entries = PackageLibrary.ListEntries(bundleFile);
        var manifest = entries.FirstOrDefault(e => e.Equals("collection.json", StringComparison.OrdinalIgnoreCase))
            ?? entries.Where(e => e.EndsWith("/collection.json", StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.Length).FirstOrDefault();
        if (manifest is null) return api;
        var bytes = PackageLibrary.ReadEntry(bundleFile, manifest);
        if (bytes is null) return api;
        try { return CollectionLogic.FromManifest(api, System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF')); }
        catch (System.Text.Json.JsonException) { return api; }
    }

    /// <summary>A load order the collection ships as archive/pc/mod/modlist.txt inside one of its mods (archive names, first wins).</summary>
    public static IReadOnlyList<string> ModlistInArchive(string archiveFile)
    {
        try
        {
            var path = PackageLibrary.ListEntries(archiveFile).FirstOrDefault(e => e.EndsWith("archive/pc/mod/modlist.txt", StringComparison.OrdinalIgnoreCase));
            if (path is null) return [];
            var bytes = PackageLibrary.ReadEntry(archiveFile, path);
            return bytes is null ? [] : System.Text.Encoding.UTF8.GetString(bytes).Split('\n')
                .Select(l => l.Trim().TrimStart('\uFEFF')).Where(l => l.Length > 0 && !l.StartsWith('#') && l.EndsWith(".archive", StringComparison.OrdinalIgnoreCase)).ToList();
        }
        catch (ImportException) { return []; }
    }

    /// <summary>A mod that came inside the collection bundle, as a file to import (null when not found).</summary>
    public string? ExtractBundled(string bundleFile, CollectionItem item)
    {
        var name = item.LogicalFileName ?? item.Name;
        var entries = PackageLibrary.ListEntries(bundleFile);
        Directory.CreateDirectory(Library.Downloads);
        var file = entries.FirstOrDefault(e => e.StartsWith("bundled/", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(e).Equals(name, StringComparison.OrdinalIgnoreCase) && Path.GetExtension(e).ToLowerInvariant() is ".zip" or ".7z" or ".rar");
        if (file is not null)
        {
            var bytes = PackageLibrary.ReadEntry(bundleFile, file);
            if (bytes is null) return null;
            var dest = Path.Combine(Library.Downloads, NexusClient.SafeFileName(Path.GetFileName(file)));
            File.WriteAllBytes(dest, bytes);
            return dest;
        }
        var folder = "bundled/" + name;
        if (!entries.Any(e => e.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))) return null;
        var zip = Path.Combine(Library.Downloads, NexusClient.SafeFileName(name) + ".zip");
        if (File.Exists(zip)) File.Delete(zip);
        return PackageLibrary.RepackFolder(bundleFile, folder, zip) > 0 ? zip : null;
    }

    // ---------------- FOMOD (R3) ----------------
    public FomodConfig ReadFomod(string archiveFile, string configPath)
    {
        var bytes = PackageLibrary.ReadEntry(archiveFile, configPath) ?? throw new ImportException("the installer description is missing in the archive");
        var text = DecodeXml(bytes);
        try { return Fomod.Parse(text); }
        catch (System.Xml.XmlException ex) { throw new ImportException("the installer description cannot be read: " + ex.Message); }
    }

    /// <summary>FOMOD texts are often UTF-16 with a BOM; otherwise UTF-8.</summary>
    private static string DecodeXml(byte[] b)
    {
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return System.Text.Encoding.Unicode.GetString(b, 2, b.Length - 2);
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return System.Text.Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return System.Text.Encoding.UTF8.GetString(b, 3, b.Length - 3);
        if (b.Length >= 2 && b[1] == 0) return System.Text.Encoding.Unicode.GetString(b);
        return System.Text.Encoding.UTF8.GetString(b);
    }

    public static byte[]? ReadFomodImage(string archiveFile, string configPath, string imagePath)
    {
        var root = Fomod.RootOf(configPath);
        var path = (root.Length == 0 ? "" : root + "/") + imagePath.Replace('\\', '/');
        try { return PackageLibrary.ReadEntry(archiveFile, path); }
        catch (ImportException) { return null; }
    }

    // ---------------- storage (P57) ----------------
    public StorageInfo StorageInfo()
    {
        var removed = Directory.Exists(Removed.Root) ? Directory.EnumerateDirectories(Removed.Root).Count() : 0;
        var (dl, dlBytes) = Afterlife.Infrastructure.Storage.FilesIn(Library.Downloads);
        return new StorageInfo(removed, Afterlife.Infrastructure.Storage.SizeOf(Removed.Root), dl, dlBytes,
            Afterlife.Infrastructure.Storage.SizeOf(Library.Staging) + Afterlife.Infrastructure.Storage.SizeOf(Library.Backups));
    }

    /// <summary>P57: deletes kept files for good. Never touches the game, the mods in the library or an unfinished change.</summary>
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

    // ---------------- load order (P25) ----------------
    /// <summary>
    /// Drag-and-drop order of a profile: top loads first and wins archive conflicts. The order of the profile entries
    /// follows the list and AFTERLIFE writes archive/pc/mod/modlist.txt from it. Archives installed outside AFTERLIFE
    /// load after the listed ones; an existing modlist.txt is kept as a baseline and restored later.
    /// </summary>
    public ProfileState Reorder(string profileId, IReadOnlyList<string> orderedPackageIds)
    {
        var p = Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        if (p.IsSystemModsOff) throw new InvalidOperationException("Mods off cannot be edited; select or create another profile");
        var packages = Store.GetPackages().ToDictionary(x => x.PackageId);
        var ordered = orderedPackageIds.Where(packages.ContainsKey).Distinct().ToList();
        var byId = p.Entries.ToDictionary(e => e.PackageId);
        var entries = ordered.Select(id => byId.TryGetValue(id, out var e) ? e : new ProfileEntry(id, false)).ToList();
        entries.AddRange(p.Entries.Where(e => !ordered.Contains(e.PackageId)));
        var archives = ordered
            .SelectMany(id => packages[id].Files.Select(f => GamePath.Parse(f.Target))
                .Where(g => string.Equals(g.ParentValue, "archive/pc/mod", StringComparison.OrdinalIgnoreCase) && g.Extension == ".archive")
                .Select(g => g.FileName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var q = p with
        {
            Entries = entries,
            ArchiveOrder = new ArchiveOrderSettings(true, archives, true, true),
            Revision = p.Revision + 1,
        };
        Store.SaveProfile(q);
        return q;
    }

    /// <summary>Back to the game's default (alphabetical) archive order.</summary>
    public ProfileState ResetOrder(string profileId)
    {
        var p = Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        if (p.IsSystemModsOff) throw new InvalidOperationException("Mods off cannot be edited; select or create another profile");
        var q = p.WithArchiveOrder(ArchiveOrderSettings.Default);
        Store.SaveProfile(q);
        return q;
    }

    // ---------------- planning ----------------
    public PreviewResult Preview(string? profileId = null, PlanDecisions? decisions = null)
    {
        var profile = profileId is null ? SelectedProfile : Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        var scan = Scan();
        var snapshot = BuildSnapshot(profile, scan);
        return MakePreview(snapshot, scan, decisions);
    }

    private PreviewResult MakePreview(DeploymentSnapshot snapshot, ScanResult scan, PlanDecisions? decisions)
    {
        var ledger = Store.GetLedger();
        var observer = new GameStateObserver(GameRoot, _fs);
        var plan = Planner.Create(snapshot, ledger, observer, Store.GetBaseline(ModlistFormat.RelativePath), decisions);
        return new PreviewResult(snapshot, plan, scan, EnvironmentIssues());
    }

    private DeploymentSnapshot BuildSnapshot(ProfileState profile, ScanResult scan)
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

    // ---------------- apply ----------------
    public ApplyResult Apply(string? profileId = null, PlanDecisions? decisions = null, IProgress<DeployProgress>? progress = null)
    {
        using var _ = InstallLock.Acquire(Library, GameRoot);
        progress?.Report(new DeployProgress(DeployPhase.Preparing, 0, 0));
        var preview = Preview(profileId, decisions);
        if (!preview.CanApply)
            return new ApplyResult(preview, null, "blocked: " + string.Join("; ", preview.Plan.Blockers.Concat(preview.EnvironmentIssues.Where(i => i.Severity == Severity.Blocking)).Select(i => i.Message)));
        if (preview.Plan.IsNoop && preview.Snapshot.ProfileId == Store.GetLastGeneration()?.ProfileId)
            return new ApplyResult(preview, null, "already deployed");
        var exec = Run(preview.Snapshot, preview.Plan, progress);
        return new ApplyResult(preview, exec, exec.Status == OperationStatus.Verified ? "deployed" : exec.Error ?? exec.Status.ToString());
    }

    private ExecutionResult Run(DeploymentSnapshot snapshot, Plan plan, IProgress<DeployProgress>? progress = null)
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

    // ---------------- recovery ----------------
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
        var scan = Scan();
        var preview = MakePreview(target with { Id = "snap-" + Guid.NewGuid().ToString("N")[..12] }, scan, null);
        if (!preview.CanApply)
            return (outcome, new ApplyResult(preview, null, "the record of what is in the game was repaired, but the game could not be brought back yet: " +
                string.Join("; ", preview.Plan.Blockers.Concat(preview.EnvironmentIssues.Where(i => i.Severity == Severity.Blocking)).Select(i => i.Message))));
        var exec = Run(preview.Snapshot, preview.Plan, progress);
        return (outcome, new ApplyResult(preview, exec, exec.Status == OperationStatus.Verified ? "recovered" : exec.Error ?? exec.Status.ToString()));
    }

    // ---------------- launch ----------------
    // ---------------- REDmod (R2) ----------------
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
    /// R2: REDmods only work after redMod.exe compiled them and the game starts with -modded. Runs the deploy when
    /// the REDmods or their files changed since the last successful run (like Vortex does on launch).
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

    public void Dispose() => Store.Dispose();
}
