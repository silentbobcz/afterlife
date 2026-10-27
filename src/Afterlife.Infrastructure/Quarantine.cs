using Afterlife.Core;

namespace Afterlife.Infrastructure;

public sealed record RemovedFile(string Path, string Hash, long Size, bool Restored = false);

public sealed record RemovedBatch(string Id, string Label, DateTimeOffset RemovedAt, IReadOnlyList<RemovedFile> Files)
{
    public int Pending => Files.Count(f => !f.Restored);
}

public sealed record RemovalResult(string? BatchId, int Moved, IReadOnlyList<string> Skipped);

/// <summary>
/// Moves files that AFTERLIFE does not manage out of the game into <c>library/removed/&lt;batch&gt;</c> (P6),
/// so that "remove" is always reversible. Copy → verify hash → delete original; the manifest is written first,
/// so after a crash every file is still in the game, in the quarantine, or in both.
/// </summary>
public sealed class Quarantine(LibraryPaths lib, IFileOps fs)
{
    public string Root => Path.Combine(lib.Root, "removed");

    private string BatchDir(string id) => Path.Combine(Root, id);
    private string Manifest(string id) => Path.Combine(BatchDir(id), "manifest.json");
    private string Stored(string id, string path) => Path.Combine(BatchDir(id), "files", path.Replace('/', Path.DirectorySeparatorChar));

    public IReadOnlyList<RemovedBatch> List()
    {
        if (!Directory.Exists(Root)) return [];
        var list = new List<RemovedBatch>();
        foreach (var dir in Directory.EnumerateDirectories(Root))
        {
            var m = Path.Combine(dir, "manifest.json");
            if (!File.Exists(m)) continue;
            try
            {
                var b = Json.Deserialize<RemovedBatch>(File.ReadAllText(m));
                if (b.Pending > 0) list.Add(b);
            }
            catch (System.Text.Json.JsonException) { /* unreadable manifest: leave the folder for manual inspection */ }
        }
        return list.OrderByDescending(b => b.RemovedAt).ToList();
    }

    /// <summary>P57: deletes every removed batch for good (they can no longer be restored). Returns bytes freed.</summary>
    public long PurgeAll()
    {
        if (!Directory.Exists(Root)) return 0;
        long freed = 0;
        foreach (var dir in Directory.EnumerateDirectories(Root))
        {
            var size = Storage.SizeOf(dir);
            try { Directory.Delete(dir, true); freed += size; }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return freed;
    }

    public RemovalResult Remove(string gameRoot, IReadOnlyCollection<GamePath> paths, string label)
    {
        var id = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        var files = new List<RemovedFile>();
        var skipped = new List<string>();
        foreach (var p in paths)
        {
            var os = p.ToOsPath(gameRoot);
            fs.EnsureNoReparsePoints(gameRoot, os);
            var meta = fs.GetMeta(os);
            if (meta is null) { skipped.Add(p.Value); continue; }
            files.Add(new RemovedFile(p.Value, fs.Hash(os), meta.Size));
        }
        if (files.Count == 0) return new RemovalResult(null, 0, skipped);

        Directory.CreateDirectory(BatchDir(id));
        WriteManifest(new RemovedBatch(id, label, DateTimeOffset.Now, files));

        int moved = 0;
        foreach (var f in files)
        {
            var gp = GamePath.Parse(f.Path);
            var src = gp.ToOsPath(gameRoot);
            var dst = Stored(id, f.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            var tmp = dst + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
            fs.CopyToTemp(src, tmp);
            if (fs.Hash(tmp) != f.Hash || fs.Hash(src) != f.Hash)
            {
                File.Delete(tmp);
                skipped.Add(f.Path); // changed while copying: leave it in the game
                continue;
            }
            File.Move(tmp, dst);
            fs.Delete(src);
            moved++;
            PruneEmptyDirectories(gameRoot, gp);
        }

        var kept = files.Where(f => !skipped.Contains(f.Path)).ToList();
        if (kept.Count == 0) { TryDeleteTree(BatchDir(id)); return new RemovalResult(null, 0, skipped); }
        WriteManifest(new RemovedBatch(id, label, DateTimeOffset.Now, kept));
        return new RemovalResult(id, moved, skipped);
    }

    /// <summary>Puts files back where they were. Never overwrites: an occupied target is skipped and reported.</summary>
    public RemovalResult Restore(string gameRoot, string batchId)
    {
        var m = Manifest(batchId);
        if (!File.Exists(m)) throw new InvalidOperationException($"no removed batch {batchId}");
        var batch = Json.Deserialize<RemovedBatch>(File.ReadAllText(m));
        var skipped = new List<string>();
        var updated = new List<RemovedFile>();
        int restored = 0;
        foreach (var f in batch.Files)
        {
            if (f.Restored) { updated.Add(f); continue; }
            var gp = GamePath.Parse(f.Path);
            var target = gp.ToOsPath(gameRoot);
            var stored = Stored(batchId, f.Path);
            if (!File.Exists(stored)) { skipped.Add(f.Path); updated.Add(f); continue; }
            if (fs.FileExists(target)) { skipped.Add(f.Path); updated.Add(f); continue; }
            var dir = Path.GetDirectoryName(target)!;
            fs.EnsureNoReparsePoints(gameRoot, dir);
            if (!fs.DirectoryExists(dir)) fs.CreateDirectory(dir);
            var tmp = target + ".afl-restore.tmp";
            if (fs.FileExists(tmp)) fs.Delete(tmp);
            fs.CopyToTemp(stored, tmp);
            if (fs.Hash(tmp) != f.Hash) { fs.Delete(tmp); skipped.Add(f.Path); updated.Add(f); continue; }
            fs.MoveNoOverwrite(tmp, target);
            File.Delete(stored);
            updated.Add(f with { Restored = true });
            restored++;
        }
        var after = batch with { Files = updated };
        if (after.Pending == 0) TryDeleteTree(BatchDir(batchId));
        else WriteManifest(after);
        return new RemovalResult(batchId, restored, skipped);
    }

    private void WriteManifest(RemovedBatch b)
    {
        var m = Manifest(b.Id);
        var tmp = m + ".tmp";
        File.WriteAllText(tmp, Json.Serialize(b));
        File.Move(tmp, m, overwrite: true);
    }

    /// <summary>Removes folders emptied by the move, but never the scanned roots such as archive/pc/mod.</summary>
    private void PruneEmptyDirectories(string gameRoot, GamePath removed)
    {
        var roots = InstallCoordinator.ScannedFolders.Select(r => GamePath.Parse(r).Key).ToHashSet(StringComparer.Ordinal);
        var dir = removed.ParentValue;
        while (!string.IsNullOrEmpty(dir))
        {
            var gp = GamePath.Parse(dir);
            if (roots.Contains(gp.Key) || roots.Any(r => r.StartsWith(gp.Key + "/", StringComparison.Ordinal))) break;
            var os = gp.ToOsPath(gameRoot);
            if (!fs.DirectoryExists(os) || !fs.IsDirectoryEmpty(os)) break;
            fs.DeleteDirectory(os);
            dir = gp.ParentValue;
        }
    }

    private static void TryDeleteTree(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }
        catch (IOException) { }
    }
}


/// <summary>P57: what AFTERLIFE keeps besides the mods themselves, and cleaning it.</summary>
public sealed record StorageInfo(int RemovedBatches, long RemovedBytes, int Downloads, long DownloadBytes, long TempBytes);

public static class Storage
{
    public static long SizeOf(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        long total = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                try { total += new FileInfo(f).Length; } catch (IOException) { }
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
        return total;
    }

    public static (int Count, long Bytes) FilesIn(string dir)
    {
        if (!Directory.Exists(dir)) return (0, 0);
        var files = Directory.EnumerateFiles(dir).ToList();
        return (files.Count, files.Sum(f => { try { return new FileInfo(f).Length; } catch (IOException) { return 0L; } }));
    }

    /// <summary>Deletes the contents of a folder, keeping the folder. Returns bytes freed.</summary>
    public static long Empty(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        long freed = 0;
        foreach (var f in Directory.EnumerateFiles(dir))
            try { var n = new FileInfo(f).Length; File.Delete(f); freed += n; } catch (IOException) { } catch (UnauthorizedAccessException) { }
        foreach (var d in Directory.EnumerateDirectories(dir))
            try { var n = SizeOf(d); Directory.Delete(d, true); freed += n; } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return freed;
    }
}
