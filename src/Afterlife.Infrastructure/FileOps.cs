using Afterlife.Core;

namespace Afterlife.Infrastructure;

public sealed record FileMeta(long Size, long MTimeTicks);

/// <summary>All filesystem access used by deployment. Replaceable in tests for fault injection.</summary>
public interface IFileOps
{
    bool FileExists(string path);
    FileMeta? GetMeta(string path);
    string Hash(string path);
    byte[] ReadAllBytes(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    bool IsDirectoryEmpty(string path);
    void DeleteDirectory(string path);
    IEnumerable<string> EnumerateFiles(string directory);
    /// <summary>Copies to a new temp file (fails if it exists) and flushes it to disk.</summary>
    void CopyToTemp(string source, string temp);
    void WriteToTemp(string temp, byte[] content);
    /// <summary>Atomic rename that fails if the target exists.</summary>
    void MoveNoOverwrite(string temp, string target);
    /// <summary>Atomic rename over an existing target.</summary>
    void MoveReplace(string temp, string target);
    void Delete(string path);
    /// <summary>Throws if any existing directory between root (exclusive) and the path is a reparse point / symlink / junction.</summary>
    void EnsureNoReparsePoints(string root, string fullPath);
}

public sealed class PathSafetyException(string message) : IOException(message);

public sealed class PhysicalFileOps : IFileOps
{
    public bool FileExists(string path) => File.Exists(path);

    public FileMeta? GetMeta(string path)
    {
        var fi = new FileInfo(path);
        return fi.Exists ? new FileMeta(fi.Length, fi.LastWriteTimeUtc.Ticks) : null;
    }

    public string Hash(string path)
    {
        using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        return ContentHash.Of(s);
    }

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool IsDirectoryEmpty(string path) => !Directory.EnumerateFileSystemEntries(path).Any();
    public void DeleteDirectory(string path) => Directory.Delete(path, recursive: false);

    public IEnumerable<string> EnumerateFiles(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true })
            : [];

    public void CopyToTemp(string source, string temp)
    {
        using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        using var dst = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16);
        src.CopyTo(dst, 1 << 20);
        dst.Flush(flushToDisk: true);
    }

    public void WriteToTemp(string temp, byte[] content)
    {
        using var dst = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        dst.Write(content);
        dst.Flush(flushToDisk: true);
    }

    public void MoveNoOverwrite(string temp, string target)
    {
        if (File.Exists(target) || Directory.Exists(target))
            throw new IOException($"target appeared before rename: {target}");
        File.Move(temp, target, overwrite: false);
    }

    public void MoveReplace(string temp, string target) => File.Move(temp, target, overwrite: true);

    public void Delete(string path)
    {
        var fi = new FileInfo(path);
        if (fi.Exists && fi.IsReadOnly) fi.IsReadOnly = false;
        File.Delete(path);
    }

    public void EnsureNoReparsePoints(string root, string fullPath)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(fullPath);
        if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new PathSafetyException($"path escapes the game folder: {fullPath}");
        var rel = full[(rootFull.Length + 1)..];
        var parts = rel.Split(Path.DirectorySeparatorChar);
        var current = rootFull;
        for (int i = 0; i < parts.Length; i++)
        {
            current = Path.Combine(current, parts[i]);
            var isLast = i == parts.Length - 1;
            FileSystemInfo info = isLast && File.Exists(current) ? new FileInfo(current) : new DirectoryInfo(current);
            if (!info.Exists) { if (isLast) break; else break; }
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget is not null)
                throw new PathSafetyException($"refusing to write through a link or junction: {current}");
        }
    }
}
