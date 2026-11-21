using Afterlife.Core;
using System.Text.RegularExpressions;

namespace Afterlife.Infrastructure;

/// <summary>One file Nexus Mods holds with the same content as a local archive.</summary>
public sealed record NexusMd5Match(string ModId, string? ModName, string FileId, string? FileName, string? Version);

/// <summary>What proved that a library package is a certain file on Nexus Mods.</summary>
public enum LinkEvidence
{
    /// <summary>Nexus has a file with exactly the same content.</summary>
    Content,
    /// <summary>The mod named in the archive name lists a file with exactly that name.</summary>
    FileName,
    /// <summary>The mod named in the archive name has a file uploaded at the second written in the name.</summary>
    UploadTime,
}

public sealed record NexusLinkMatch(string PackageId, string ModId, string? FileId, string? ModName, LinkEvidence Evidence)
{
    public string EvidenceLabel => Evidence switch
    {
        LinkEvidence.Content => "same file",
        LinkEvidence.FileName => "same file name",
        _ => "same upload time",
    };
}

/// <summary>
/// Links mods added from archives on disk to their page on Nexus Mods. The archive name only gives a guess
/// (<see cref="PackageLibrary.GuessFromFileName"/>); a link is made only when Nexus confirms it, because a wrong
/// link shows the description and requirements of another mod and offers its updates.
/// </summary>
public static class NexusLinker
{
    private static string Key(string packageId) => "nexus:linktry:" + packageId;

    /// <summary>Time for Nexus to learn about a file before a package without a match is asked about again.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(24);

    private static bool Linkable(PackageManifest p) =>
        p.LayerOfModId is null && p.Origin == PackageOrigin.Manual && !NexusInfo.IdOf(p).Confirmed
        && !p.ModId.StartsWith("fixer-", StringComparison.Ordinal);

    /// <summary>Packages added by hand that are not linked yet and carry something to ask Nexus about.</summary>
    public static IReadOnlyList<PackageManifest> Candidates(IEnumerable<PackageManifest> packages) =>
        packages.Where(p => Linkable(p) && (p.SourceMd5 is not null || p.NexusModId is not null)).ToList();

    /// <summary>Packages added by hand with neither a checksum of their archive nor an id in its name.</summary>
    public static IReadOnlyList<PackageManifest> WithoutHint(IEnumerable<PackageManifest> packages) =>
        packages.Where(p => Linkable(p) && p.SourceMd5 is null && p.NexusModId is null).ToList();

    /// <summary>
    /// The mod a content search points to. Several mods can carry the same file; then only the one named in the
    /// archive name counts, and without it nothing does.
    /// </summary>
    public static NexusLinkMatch? FromContent(PackageManifest p, IReadOnlyList<NexusMd5Match> hits)
    {
        if (hits.Count == 0) return null;
        var mods = hits.Select(h => h.ModId).Distinct(StringComparer.Ordinal).ToList();
        var modId = mods.Count == 1 ? mods[0] : mods.FirstOrDefault(m => m == p.NexusModId);
        if (modId is null) return null;
        var ofMod = hits.Where(h => h.ModId == modId).ToList();
        var hit = ofMod.FirstOrDefault(h => SameName(h.FileName, p.SourceArchiveName)) ?? ofMod[0];
        // two different files of the mod with this content: the mod is certain, the file is not
        var fileId = ofMod.Select(h => h.FileId).Distinct(StringComparer.Ordinal).Count() == 1 || SameName(hit.FileName, p.SourceArchiveName) ? hit.FileId : null;
        return new NexusLinkMatch(p.PackageId, modId, fileId, hit.ModName, LinkEvidence.Content);
    }

    /// <summary>The file of the guessed mod that the archive name describes: the same name, or the same upload second.</summary>
    public static NexusLinkMatch? FromFiles(PackageManifest p, NexusFiles files, string? modName = null)
    {
        if (p.NexusModId != files.ModId || p.SourceArchiveName is null) return null;
        var byName = files.Files.Where(f => SameName(f.FileName, p.SourceArchiveName)).ToList();
        if (byName.Count == 1) return new NexusLinkMatch(p.PackageId, files.ModId, byName[0].FileId, modName, LinkEvidence.FileName);
        if (UploadTimeOf(p.SourceArchiveName) is { } time)
        {
            var byTime = files.Files.Where(f => f.UploadedAt == time).ToList();
            if (byTime.Count == 1) return new NexusLinkMatch(p.PackageId, files.ModId, byTime[0].FileId, modName, LinkEvidence.UploadTime);
        }
        return null;
    }

    /// <summary>
    /// Asks Nexus about one package: first for a file with the same content, then for the file its archive name
    /// describes. Null when Nexus confirms nothing. A reached rate limit and an unreachable Nexus are thrown.
    /// </summary>
    public static async Task<NexusLinkMatch?> VerifyAsync(NexusClient client, PackageManifest p, CancellationToken ct = default)
    {
        if (p.SourceMd5 is { Length: 32 } md5 && FromContent(p, await client.Md5SearchAsync(md5, ct)) is { } byContent) return byContent;
        if (p.NexusModId is not { Length: > 0 } guess || p.SourceArchiveName is null) return null;
        NexusFiles files;
        try { files = await client.FilesAsync(guess, ct); }
        catch (NexusException ex) when (ex.NotFound) { return null; }
        return FromFiles(p, files);
    }

    /// <summary>Remembers that Nexus confirmed nothing for the package, so the next run does not ask again at once.</summary>
    public static void MarkTried(AfterlifeStore store, string packageId, DateTimeOffset now) =>
        store.SetMeta(Key(packageId), now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Package ids Nexus confirmed nothing for within <see cref="RetryAfter"/>.</summary>
    public static HashSet<string> TriedRecently(AfterlifeStore store, DateTimeOffset now)
    {
        var prefix = Key("");
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, value) in store.GetMetaByPrefix(prefix))
            if (long.TryParse(value, out var t) && now - DateTimeOffset.FromUnixTimeSeconds(t) < RetryAfter) result.Add(key[prefix.Length..]);
        return result;
    }

    /// <summary>Browsers add " (1)" to a second download of the same file; Nexus never has that in a name.</summary>
    private static bool SameName(string? nexusName, string? archiveName)
    {
        if (string.IsNullOrWhiteSpace(nexusName) || string.IsNullOrWhiteSpace(archiveName)) return false;
        return Plain(nexusName).Equals(Plain(archiveName), StringComparison.OrdinalIgnoreCase);

        static string Plain(string name)
        {
            var stem = Regex.Replace(Path.GetFileNameWithoutExtension(name), @"\s*\(\d{1,3}\)$", "");
            return stem.Trim() + Path.GetExtension(name);
        }
    }

    /// <summary>The upload time at the end of an older Nexus file name ("Name-1234-1-2-3-1700000000.zip").</summary>
    private static long? UploadTimeOf(string archiveName)
    {
        var stem = Regex.Replace(Path.GetFileNameWithoutExtension(archiveName), @"\s*\(\d{1,3}\)$", "");
        var m = Regex.Match(stem, @"-\d{1,7}-.*-(?<ts>\d{9,11})$");
        return m.Success && long.TryParse(m.Groups["ts"].Value, out var t) ? t : null;
    }
}
