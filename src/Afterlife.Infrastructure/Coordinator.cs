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
    /// <summary>Files the deleted mods had created themselves, moved to Removed.</summary>
    public int Leftovers { get; init; }
}

public sealed record Dependent(string PackageId, string Name, IReadOnlyList<string> Needs, IReadOnlyList<string> Profiles, bool InGame);

public sealed record ScanResult(IReadOnlyList<(string Path, long Size)> Unmanaged, IReadOnlySet<string> ExternalCapabilities, IReadOnlyList<string> UnmanagedArchiveNames)
{
    /// <summary>Version read from the binary of each framework installed outside AFTERLIFE (null when it has none).</summary>
    public IReadOnlyDictionary<string, string?> ExternalVersions { get; init; } = new Dictionary<string, string?>();

    /// <summary>Unmanaged file → library mods that want to write the same path.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> InTheWay { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    /// <summary>Files a mod installed by AFTERLIFE created by itself (logs, settings, caches): path → package id. Not listed as manual mods.</summary>
    public IReadOnlyDictionary<string, string> RuntimeFiles { get; init; } = new Dictionary<string, string>();

    /// <summary>Unmanaged files grouped into likely mods.</summary>
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
/// <summary>
/// The single entry point for the app: owns the library, the store and the game root and hands the work to the
/// services (<see cref="ProfileService"/>, <see cref="ScanService"/>, <see cref="LibraryService"/>,
/// <see cref="DeployService"/>, <see cref="BundleService"/>). The members below only forward to them.
/// </summary>
public sealed class InstallCoordinator : IDisposable
{
    private readonly InstallContext _ctx;

    public LibraryPaths Library => _ctx.LibraryPaths;
    public AfterlifeStore Store => _ctx.Store;
    public PackageLibrary Packages => _ctx.Packages;
    public string GameRoot => _ctx.GameRoot;
    public Quarantine Removed => _ctx.Removed;

    public ProfileService ProfileService { get; }
    public ScanService ScanService { get; }
    public LibraryService LibraryService { get; }
    public DeployService DeployService { get; }
    public BundleService BundleService { get; }

    private InstallCoordinator(LibraryPaths lib, AfterlifeStore store, IFileOps fs, IGameEnvironment env)
    {
        _ctx = new InstallContext(lib, store, fs, env);
        ProfileService = new ProfileService(_ctx);
        ScanService = new ScanService(_ctx);
        LibraryService = new LibraryService(_ctx);
        DeployService = new DeployService(_ctx);
        BundleService = new BundleService(_ctx);
        _ctx.Profiles = ProfileService;
        _ctx.Scanner = ScanService;
        _ctx.Library = LibraryService;
        _ctx.Deployer = DeployService;
        _ctx.Bundles = BundleService;
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
        var fp = c._ctx.Env.Fingerprint(full);
        if (fp is not null && store.GetMeta("game.fingerprint.ack") is null) store.SetMeta("game.fingerprint.ack", Json.Serialize(fp));
        return c;
    }

    public static InstallCoordinator Open(string libraryRoot, IFileOps? fs = null, IGameEnvironment? env = null)
    {
        var lib = new LibraryPaths(libraryRoot);
        if (!File.Exists(lib.Database)) throw new FileNotFoundException("no AFTERLIFE library here; run init first", lib.Database);
        return new InstallCoordinator(lib, AfterlifeStore.Open(lib.Database), fs ?? new PhysicalFileOps(), env ?? new WindowsGameEnvironment());
    }

    public IReadOnlyList<ProfileState> Profiles => Store.GetProfiles();
    public ProfileState SelectedProfile => _ctx.SelectedProfile;

    /// <summary>
    /// Points the library at another game folder. Allowed when nothing is deployed (apply Mods off first),
    /// Or when the same install was moved and every managed file is found unchanged at the new place.
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
        if (_ctx.Env.IsGameRunning()) throw new InvalidOperationException("Cyberpunk 2077 is running; close it first");
        var ledger = Store.GetLedger();
        var moved = ledger.Count > 0 && ledger.Values.All(l =>
        {
            var os = GamePath.Parse(l.Path).ToOsPath(full);
            return _ctx.Fs.FileExists(os) && _ctx.Fs.Hash(os) == l.Hash;
        });
        if (ledger.Count > 0 && !moved)
            throw new InvalidOperationException("mods are deployed in the current folder; select Mods off and apply first, then change the folder");
        if (Store.GetBaseline(ModlistFormat.RelativePath) is not null && !moved)
            throw new InvalidOperationException("AFTERLIFE still keeps modlist.txt of the current folder; select Mods off and apply first");
        Store.SetMeta("game.root", full);
        _ctx.GameRoot = full;
        var fp = _ctx.Env.Fingerprint(full);
        if (fp is not null && !moved) Store.SetMeta("game.fingerprint.ack", Json.Serialize(fp));
        ScanService.Scan();
    }

    // ---- ProfileService ----
    public void SelectProfile(string profileId) => ProfileService.SelectProfile(profileId);
    public ProfileState CreateProfile(string name, string? copyFromId = null) => ProfileService.CreateProfile(name, copyFromId);
    public void DeleteProfile(string profileId) => ProfileService.DeleteProfile(profileId);
    public ProfileState UpdateSelected(Func<ProfileState, ProfileState> change) => ProfileService.UpdateSelected(change);
    public ProfileState SetEnabled(string packageId, bool enabled) => ProfileService.SetEnabled(packageId, enabled);
    public ProfileState RenameProfile(string profileId, string name) => ProfileService.RenameProfile(profileId, name);
    public ProfileState SetEnabledMany(string profileId, IReadOnlyCollection<string> packageIds, bool enabled) => ProfileService.SetEnabledMany(profileId, packageIds, enabled);
    public ProfileState Reorder(string profileId, IReadOnlyList<string> orderedPackageIds) => ProfileService.Reorder(profileId, orderedPackageIds);
    public ProfileState ResetOrder(string profileId) => ProfileService.ResetOrder(profileId);

    // ---- ScanService ----
    public static string[] ScannedFolders => ScanService.ScannedFolders;
    public ScanResult Scan() => ScanService.Scan();
    public ScanResult CachedScan() => ScanService.CachedScan();
    public void InvalidateScan() => ScanService.InvalidateScan();
    public static bool IsProtectedGameFile(string path) => ScanService.IsProtectedGameFile(path);
    public IReadOnlyList<string> ForeignFilesOf(string packageId) => ScanService.ForeignFilesOf(packageId);
    public RemovalResult MoveAsideFor(string packageId) => ScanService.MoveAsideFor(packageId);
    public static (string Signature, string[] Extras)[] FrameworkExtras => ScanService.FrameworkExtras;
    public static bool IsRemovablePath(string path) => ScanService.IsRemovablePath(path);
    public RemovalResult RemoveUnmanaged(IReadOnlyCollection<string> paths, string label) => ScanService.RemoveUnmanaged(paths, label);
    public RemovalResult RestoreRemoved(string batchId) => ScanService.RestoreRemoved(batchId);
    public RemovalResult? CleanLeftovers(string capability) => ScanService.CleanLeftovers(capability);

    // ---- LibraryService ----
    public PackageManifest RenamePackage(string packageId, string? name) => LibraryService.RenamePackage(packageId, name);
    public string OriginalName(string packageId) => LibraryService.OriginalName(packageId);
    public ImportResult Import(ImportRequest request) => LibraryService.Import(request);
    public IReadOnlyList<IReadOnlyList<PackageManifest>> DuplicateGroups() => LibraryService.DuplicateGroups();
    public void AdoptDuplicates(string keepId, IReadOnlyCollection<string> others) => LibraryService.AdoptDuplicates(keepId, others);
    public void ReplaceVersion(string oldId, string newId) => LibraryService.ReplaceVersion(oldId, newId);
    public void ReplaceInCollections(string oldId, string newId) => LibraryService.ReplaceInCollections(oldId, newId);
    public void RemoveFromLibrary(string packageId) => LibraryService.RemoveFromLibrary(packageId);
    public PackageManifest CaptureChanges(IReadOnlyCollection<string> paths) => LibraryService.CaptureChanges(paths);
    public (IReadOnlyList<string> Deleted, IReadOnlyList<(string Name, string Reason)> Kept) DeleteFromLibrary(IReadOnlyCollection<string> packageIds) => LibraryService.DeleteFromLibrary(packageIds);
    public DeleteOutcome DeleteEverywhere(IReadOnlyCollection<string> packageIds, IProgress<DeployProgress>? progress = null) => LibraryService.DeleteEverywhere(packageIds, progress);
    public IReadOnlyList<Dependent> DependentsOf(IReadOnlyCollection<string> capabilities, IReadOnlyCollection<string>? exceptPackages = null) => LibraryService.DependentsOf(capabilities, exceptPackages);
    public void DeactivateEverywhere(IReadOnlyCollection<string> packageIds) => LibraryService.DeactivateEverywhere(packageIds);
    public int RefreshRequirements() => LibraryService.RefreshRequirements();
    public PackageManifest LinkToNexus(string packageId, string nexusModId) => LibraryService.LinkToNexus(packageId, nexusModId);
    public StorageInfo StorageInfo() => LibraryService.StorageInfo();
    public long PurgeStorage(bool removed, bool downloads, bool temp) => LibraryService.PurgeStorage(removed, downloads, temp);

    // ---- DeployService ----
    public PreviewResult Preview(string? profileId = null, PlanDecisions? decisions = null, bool rescan = true) => DeployService.Preview(profileId, decisions, rescan);
    public List<Issue> EnvironmentIssues() => DeployService.EnvironmentIssues();
    public bool BuildChanged(out GameFingerprint? current) => DeployService.BuildChanged(out current);
    public void AcknowledgeBuild() => DeployService.AcknowledgeBuild();
    public ApplyResult Apply(string? profileId = null, PlanDecisions? decisions = null, IProgress<DeployProgress>? progress = null) => DeployService.Apply(profileId, decisions, progress);
    public OperationRecord? PendingRecovery => DeployService.PendingRecovery;
    public (RecoveryOutcome Outcome, ApplyResult Followup) Recover(RecoveryChoice choice, IProgress<DeployProgress>? progress = null) => DeployService.Recover(choice, progress);
    public const string RedModTool = DeployService.RedModTool;
    public const string RedModLog = DeployService.RedModLog;
    public IReadOnlyList<string> DeployedRedMods() => DeployService.DeployedRedMods();
    public RedModResult DeployRedMods(bool force = false) => DeployService.DeployRedMods(force);
    public Process LaunchGame(string arguments = "--launcher-skip", bool allowChangedBuild = false) => DeployService.LaunchGame(arguments, allowChangedBuild);
    public StatusReport Status() => DeployService.Status();

    // ---- BundleService ----
    public IReadOnlyList<CollectionRecord> Collections() => BundleService.Collections();
    public CollectionRevisionInfo ReadCollectionBundle(CollectionRevisionInfo api, string bundleFile) => BundleService.ReadCollectionBundle(api, bundleFile);
    public static IReadOnlyList<string> ModlistInArchive(string archiveFile) => BundleService.ModlistInArchive(archiveFile);
    public string? ExtractBundled(string bundleFile, CollectionItem item) => BundleService.ExtractBundled(bundleFile, item);
    public FomodConfig ReadFomod(string archiveFile, string configPath) => BundleService.ReadFomod(archiveFile, configPath);
    public static byte[]? ReadFomodImage(string archiveFile, string configPath, string imagePath) => BundleService.ReadFomodImage(archiveFile, configPath, imagePath);

    public void Dispose() => Store.Dispose();
}

/// <summary>What every service shares: paths, store, file system, environment, the current game root and the other services.</summary>
public sealed class InstallContext
{
    internal InstallContext(LibraryPaths lib, AfterlifeStore store, IFileOps fs, IGameEnvironment env)
    {
        LibraryPaths = lib;
        Store = store;
        Fs = fs;
        Env = env;
        GameRoot = store.GetMeta("game.root") ?? throw new InvalidOperationException("library is not initialized");
        Packages = new PackageLibrary(lib, store);
        Removed = new Quarantine(lib, fs);
    }

    public LibraryPaths LibraryPaths { get; }
    public AfterlifeStore Store { get; }
    public IFileOps Fs { get; }
    public IGameEnvironment Env { get; }
    public PackageLibrary Packages { get; }
    public Quarantine Removed { get; }
    public string GameRoot { get; internal set; }

    public ProfileService Profiles { get; internal set; } = null!;
    public ScanService Scanner { get; internal set; } = null!;
    public LibraryService Library { get; internal set; } = null!;
    public DeployService Deployer { get; internal set; } = null!;
    public BundleService Bundles { get; internal set; } = null!;

    public ProfileState SelectedProfile =>
        Store.GetProfile(Store.GetMeta("profile.selected") ?? ProfileState.ModsOffId) ?? Store.GetProfile(ProfileState.ModsOffId)!;

    /// <summary>Nothing may change the game while it runs or while an operation waits for recovery.</summary>
    internal void EnsureQuiet()
    {
        if (Env.IsGameRunning()) throw new InvalidOperationException("Cyberpunk 2077 is running; close it first");
        if (Store.GetUnfinishedOperation() is not null) throw new InvalidOperationException("an operation did not finish; run recovery first");
    }
}
