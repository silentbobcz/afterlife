using System.Security.Cryptography;
using System.Text.Json;

namespace Afterlife.Infrastructure;

/// <summary>Rules for folders that must not lie in each other (the game, the library, a new place for the library).</summary>
public static class FolderRules
{
    /// <summary>Full path without a separator at its end ("C:\Games\" and "C:\Games" are the same folder).</summary>
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    /// <summary>True when <paramref name="path"/> is <paramref name="folder"/> itself or lies inside it.</summary>
    public static bool IsSameOrInside(string path, string folder)
    {
        var p = Normalize(path);
        var f = Normalize(folder);
        if (string.Equals(p, f, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = Path.EndsInDirectorySeparator(f) ? f : f + Path.DirectorySeparatorChar;
        return p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when one folder is the other or lies inside it, in either direction.</summary>
    public static bool Overlap(string a, string b) => IsSameOrInside(a, b) || IsSameOrInside(b, a);
}

public enum LibraryMoveStage { Copying, Switched }

/// <summary>The record of a move in progress, kept next to the settings so a start after a crash knows which folder counts.</summary>
public sealed record LibraryMoveState(string From, string To, LibraryMoveStage Stage);

/// <summary>A checked move, ready to run.</summary>
public sealed record LibraryMovePlan(string From, string To, long Bytes, int Files, bool SameVolume);

public sealed record LibraryMoveProgress(long DoneBytes, long TotalBytes, int DoneFiles, int TotalFiles)
{
    public int Percent => TotalBytes <= 0 ? 100 : (int)Math.Min(100, DoneBytes * 100 / TotalBytes);
}

/// <summary>What a start finds: nothing, a move that only needs its last step, or a copy that stopped half-way.</summary>
public enum LibraryMovePending { None, Finish, Interrupted }

/// <summary>
/// Moves the whole library to another folder while no AFTERLIFE has it open. The game folder is never touched:
/// deployed files are copies, so mods in the game keep working wherever the library is.
/// On the same drive the folder is renamed. Across drives every file is copied under a temporary name, checked
/// and renamed; the settings switch to the new folder only after the whole copy, and the old files are deleted
/// only after the switch. A state file records the move, so after a crash the copy can be continued or undone
/// and the library in the old folder stays complete until the switch.
/// </summary>
public sealed class LibraryMover(string stateFile, Action<string> saveLibraryRoot, Func<string, long?>? freeSpace = null)
{
    private const string PartSuffix = ".afterlife-part";
    private const string LocksFolder = "locks";

    /// <summary>Renames a folder on the same drive; false when it cannot (the mover then copies). Replaced in tests.</summary>
    public Func<string, string, bool> Rename { get; init; } = TryRename;

    /// <summary>Called before each file is copied (tests stop a copy half-way with it).</summary>
    public Action<string>? BeforeCopy { get; init; }

    public static string DefaultStateFile => Path.Combine(AppSettings.SettingsDirectory, "library-move.json");

    /// <summary>
    /// Checks a move without changing anything and returns it as a plan; throws
    /// <see cref="InvalidOperationException"/> with the reason when the move is not possible.
    /// </summary>
    public LibraryMovePlan Check(string from, string to, string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(to)) throw new InvalidOperationException("Choose a folder for the library.");
        var src = FolderRules.Normalize(from);
        var dst = FolderRules.Normalize(to);
        if (!AppSettings.IsInitialized(src)) throw new InvalidOperationException("There is no AFTERLIFE library in " + src);
        if (FolderRules.Overlap(src, dst))
            throw new InvalidOperationException("Choose a folder outside the current library folder (and not a folder that contains it).");
        if (FolderRules.Overlap(dst, gameRoot))
            throw new InvalidOperationException("The library must not be inside the game folder, and the game must not be inside the library.");
        if (File.Exists(dst)) throw new InvalidOperationException("That is a file, not a folder.");
        if (Directory.Exists(dst) && Directory.EnumerateFileSystemEntries(dst).Any())
            throw new InvalidOperationException("That folder is not empty. Choose an empty folder or type a new one.");
        EnsureWritable(dst);

        var files = FilesOf(src);
        var bytes = files.Sum(f => new FileInfo(f).Length);
        var sameVolume = string.Equals(Path.GetPathRoot(src), Path.GetPathRoot(dst), StringComparison.OrdinalIgnoreCase);
        if (!sameVolume)
        {
            var free = (freeSpace ?? FreeSpaceOf)(dst);
            var needed = bytes + bytes / 20 + (64L << 20);
            if (free is { } f && f < needed)
                throw new InvalidOperationException($"Not enough free space on that drive: the library needs {Gb(needed)}, {Gb(f)} is free.");
        }
        return new LibraryMovePlan(src, dst, bytes, files.Count, sameVolume);
    }

    /// <summary>
    /// Runs a checked move. When it throws, the library is still complete in the old folder and the unfinished
    /// copy is removed; when it returns, the settings point to the new folder.
    /// </summary>
    public void Move(LibraryMovePlan plan, IProgress<LibraryMoveProgress>? progress = null, CancellationToken ct = default)
    {
        EnsureNotInUse(plan.From);
        WriteState(new LibraryMoveState(plan.From, plan.To, LibraryMoveStage.Copying));
        if (plan.SameVolume && RenameInto(plan.From, plan.To))
        {
            Switch(plan.From, plan.To);
            return;
        }
        try { CopyAll(plan.From, plan.To, progress, ct); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            Undo();
            throw ex is OperationCanceledException ? ex : new InvalidOperationException("The library was not moved: " + ex.Message + " Everything is still in the old folder.", ex);
        }
        Switch(plan.From, plan.To);
    }

    public LibraryMoveState? Pending()
    {
        try
        {
            return File.Exists(stateFile) ? JsonSerializer.Deserialize<LibraryMoveState>(File.ReadAllText(stateFile), Json.Options) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>What is left of an earlier move. A state that no longer matches any library on disk is forgotten.</summary>
    public LibraryMovePending Inspect()
    {
        if (Pending() is not { } s) { ClearState(); return LibraryMovePending.None; }
        var source = AppSettings.IsInitialized(s.From);
        var target = AppSettings.IsInitialized(s.To);
        // switched, or renamed in one step: the new folder holds the library, only the last step is missing
        if (target && (s.Stage == LibraryMoveStage.Switched || !source)) return LibraryMovePending.Finish;
        if (source) return LibraryMovePending.Interrupted;
        ClearState();
        return LibraryMovePending.None;
    }

    /// <summary>Last step of a move that already has the library in the new folder; returns that folder.</summary>
    public string FinishPending()
    {
        var s = Pending() ?? throw new InvalidOperationException("no library move to finish");
        Switch(s.From, s.To);
        return s.To;
    }

    /// <summary>Continues a copy that stopped half-way; files already copied are not copied again.</summary>
    public void Continue(IProgress<LibraryMoveProgress>? progress = null, CancellationToken ct = default)
    {
        var s = Pending() ?? throw new InvalidOperationException("no library move to continue");
        EnsureNotInUse(s.From);
        CopyAll(s.From, s.To, progress, ct);
        Switch(s.From, s.To);
    }

    /// <summary>
    /// Gives up a copy that stopped half-way: deletes the unfinished copy and keeps the library where it is.
    /// Does nothing to the new folder unless the complete library is still in the old one.
    /// </summary>
    public void Undo()
    {
        if (Pending() is not { } s) return;
        if (s.Stage == LibraryMoveStage.Copying && AppSettings.IsInitialized(s.From) && Directory.Exists(s.To))
        {
            try { Directory.Delete(s.To, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* leftovers of the copy stay; the library is untouched */ }
        }
        ClearState();
    }

    // ---------------- steps ----------------

    private bool RenameInto(string from, string to)
    {
        try
        {
            // Check allowed an existing empty folder; a rename needs the name to be free
            if (Directory.Exists(to) && !Directory.EnumerateFileSystemEntries(to).Any()) Directory.Delete(to);
            if (Directory.Exists(to)) return false;
            var parent = Path.GetDirectoryName(to);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        return Rename(from, to);
    }

    private static bool TryRename(string from, string to)
    {
        try { Directory.Move(from, to); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private void CopyAll(string from, string to, IProgress<LibraryMoveProgress>? progress, CancellationToken ct)
    {
        var files = FilesOf(from);
        var total = files.Sum(f => new FileInfo(f).Length);
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.EnumerateDirectories(from, "*", Walk))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        long done = 0;
        var count = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            var length = new FileInfo(file).Length;
            // a finished file has its final name; one cut off by a crash still has the temporary name
            if (!(File.Exists(target) && new FileInfo(target).Length == length))
            {
                BeforeCopy?.Invoke(file);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var part = target + PartSuffix;
                if (File.Exists(part)) File.SetAttributes(part, FileAttributes.Normal);
                File.Copy(file, part, overwrite: true);
                if (new FileInfo(part).Length != length) throw new IOException("the copy of " + Path.GetFileName(file) + " is not complete");
                File.Move(part, target, overwrite: true);
            }
            done += length;
            progress?.Report(new LibraryMoveProgress(done, total, ++count, files.Count));
        }
        var db = new LibraryPaths(from).Database;
        if (!HashOf(db).SequenceEqual(HashOf(new LibraryPaths(to).Database)))
            throw new IOException("the copy of the library database does not match the original");
    }

    /// <summary>The new folder becomes the library; only then the old files go.</summary>
    private void Switch(string from, string to)
    {
        WriteState(new LibraryMoveState(from, to, LibraryMoveStage.Switched));
        saveLibraryRoot(to);
        DeleteMoved(from, to);
        ClearState();
    }

    /// <summary>
    /// Deletes from the old folder exactly the files that are in the new one with the same size, then the folders
    /// that became empty. Anything else that was put there stays.
    /// </summary>
    private static void DeleteMoved(string from, string to)
    {
        if (!Directory.Exists(from)) return;
        try
        {
            // the database first: what is left of the old folder must not look like a library
            var files = FilesOf(from).OrderBy(f => string.Equals(f, new LibraryPaths(from).Database, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
            foreach (var file in files)
            {
                var copy = Path.Combine(to, Path.GetRelativePath(from, file));
                try
                {
                    if (!File.Exists(copy) || new FileInfo(copy).Length != new FileInfo(file).Length) continue;
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            var locks = Path.Combine(from, LocksFolder);
            if (Directory.Exists(locks))
                foreach (var f in Directory.EnumerateFiles(locks, "*.lock"))
                    try { File.Delete(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            foreach (var dir in Directory.EnumerateDirectories(from, "*", Walk).OrderByDescending(d => d.Length).Append(from))
                try { if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* the old folder keeps what could not be read */ }
    }

    // ---------------- helpers ----------------

    private static readonly EnumerationOptions Walk = new() { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };

    /// <summary>Every file of the library except the lock files, which belong to a running AFTERLIFE and are made again.</summary>
    private static List<string> FilesOf(string root)
    {
        var locks = Path.Combine(root, LocksFolder) + Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(root, "*", Walk)
            .Where(f => !f.StartsWith(locks, StringComparison.OrdinalIgnoreCase) && !f.EndsWith(PartSuffix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static void EnsureWritable(string folder)
    {
        var created = new List<string>();
        try
        {
            for (var d = folder; !string.IsNullOrEmpty(d) && !Directory.Exists(d); d = Path.GetDirectoryName(d)) created.Add(d);
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, ".afterlife-write-test-" + Guid.NewGuid().ToString("N")[..6]);
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException("AFTERLIFE cannot write to that folder: " + ex.Message);
        }
        finally
        {
            // leave no trace of the check: folders made for it go again (deepest first)
            foreach (var d in created)
                try { if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>The database opens exclusively only when no AFTERLIFE window or command has the library open.</summary>
    private static void EnsureNotInUse(string libraryRoot)
    {
        try
        {
            using var _ = new FileStream(new LibraryPaths(libraryRoot).Database, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("The library is in use by another AFTERLIFE window or program. Close it and try again.");
        }
    }

    private static byte[] HashOf(string file)
    {
        using var s = File.OpenRead(file);
        return SHA256.HashData(s);
    }

    private static long? FreeSpaceOf(string folder)
    {
        try { return Path.GetPathRoot(folder) is { Length: > 0 } root ? new DriveInfo(root).AvailableFreeSpace : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    private static string Gb(long bytes) => $"{bytes / (double)(1L << 30):0.0} GB";

    private void WriteState(LibraryMoveState state)
    {
        var dir = Path.GetDirectoryName(stateFile);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = stateFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, Json.Options));
        File.Move(tmp, stateFile, overwrite: true);
    }

    private void ClearState()
    {
        try { File.Delete(stateFile); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
