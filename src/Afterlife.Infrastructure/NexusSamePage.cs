namespace Afterlife.Infrastructure;

public enum SamePageVerdict
{
    /// <summary>The file belongs beside what is in the library (core and data, optional add-ons, variants).</summary>
    Separate,
    /// <summary>The file is the next version of a package in the library.</summary>
    Replace,
    /// <summary>Nexus does not say; the user decides.</summary>
    Unknown,
}

/// <summary>
/// A Nexus page often carries several files that are installed side by side. A file arriving from a page the
/// library already knows is a new version only of the package whose file it follows.
/// </summary>
public static class NexusSamePage
{
    /// <param name="installed">Packages of this page in the library, newest first, with the Nexus file each came from.</param>
    public static (SamePageVerdict Verdict, string? PackageId) Decide(NexusFiles files, string incomingFileId,
        IReadOnlyList<(string PackageId, string? FileId)> installed)
    {
        var incoming = files.Files.FirstOrDefault(f => f.FileId == incomingFileId);
        string? sameName = null, unknown = null;
        foreach (var (packageId, fileId) in installed)
        {
            if (fileId == incomingFileId) continue;
            if (fileId is null) { unknown ??= packageId; continue; }
            var later = Later(files, fileId);
            var mine = files.Files.FirstOrDefault(f => f.FileId == fileId);
            var named = mine is not null && incoming is not null && SameName(mine.Name, incoming.Name);
            var live = mine is not null && !mine.IsArchived && !mine.Category.Equals("OLD_VERSION", StringComparison.OrdinalIgnoreCase);
            if (later.Contains(incomingFileId))
            {
                // a variant uploaded as "a new version" of another one, while both are still offered: not for us to delete
                if (live && !named) { unknown ??= packageId; continue; }
                return (SamePageVerdict.Replace, packageId);
            }
            // authors often upload the next version without marking what it replaces; the file keeps its name
            if (named) { sameName ??= packageId; continue; }
            // its line of versions goes on in another file: marked by Nexus, or a later upload under its own name
            if (later.Count > 0) continue;
            if (mine is not null && files.Files.Any(f => f.FileId != fileId && f.UploadedAt > mine.UploadedAt && SameName(f.Name, mine.Name))) continue;
            if (!live) unknown ??= packageId;
        }
        if (sameName is not null) return (SamePageVerdict.Replace, sameName);
        return unknown is not null ? (SamePageVerdict.Unknown, unknown) : (SamePageVerdict.Separate, null);
    }

    /// <summary>Every file Nexus lists as a later version of this one.</summary>
    private static HashSet<string> Later(NexusFiles files, string fileId)
    {
        var seen = new HashSet<string>();
        var todo = new Stack<string>([fileId]);
        while (todo.TryPop(out var id))
            foreach (var u in files.Updates)
                if (u.Old == id && u.New != fileId && seen.Add(u.New)) todo.Push(u.New);
        return seen;
    }

    private static bool SameName(string a, string b) =>
        a.Trim().Length > 0 && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
