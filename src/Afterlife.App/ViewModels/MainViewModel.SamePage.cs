using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.App.ViewModels;

/// <summary>A Nexus download from a page the library already has a file of: a new version, or a file beside it.</summary>
public sealed partial class MainViewModel
{
    /// <param name="Replaces">The package the arriving file is the next version of; null when it comes in as a mod of its own.</param>
    /// <param name="Name">The file's own name on the page, for a file that stays beside the others.</param>
    private sealed record Arrival(PackageManifest? Replaces, string? Name);

    private async Task<Arrival> ArrivalOfAsync(NxmLink link, NexusFileInfo info, bool quiet)
    {
        if (_overview is null) return new Arrival(null, null);
        var same = _overview.Mods.Select(m => m.Package)
            .Where(p => p.LayerOfModId is null && NexusInfo.IdOf(p).Id == link.ModId && p.NexusFileId != link.FileId)
            .OrderByDescending(p => p.ImportedAt).ToList();
        if (same.Count == 0) return new Arrival(null, null);

        // the update AFTERLIFE itself found for a package points at this very file
        var updated = _overview.Mods.FirstOrDefault(m => m.Package.LayerOfModId is null && m.Update is { } u && u.ModId == link.ModId && u.FileId == link.FileId);
        if (updated is not null) return new Arrival(updated.Package, null);

        NexusFiles? files = null;
        if (NexusKeyStore.Load() is { } key)
        {
            try
            {
                using var client = new NexusClient(key, AppVersion);
                files = await client.FilesAsync(link.ModId);
            }
            catch (Exception ex) when (ex is NexusException or System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { }
        }
        var (verdict, packageId) = files is null
            ? (SamePageVerdict.Unknown, same[0].PackageId)
            : NexusSamePage.Decide(files, link.FileId, same.Select(p => (p.PackageId, p.NexusFileId)).ToList());
        var name = files?.Files.FirstOrDefault(f => f.FileId == link.FileId)?.Name is { } n && n.Trim().Length > 0 ? n.Trim() : null;
        var older = packageId is null ? null : same.FirstOrDefault(p => p.PackageId == packageId);
        if (older is null || verdict == SamePageVerdict.Separate) return new Arrival(null, name);
        if (verdict == SamePageVerdict.Replace) return new Arrival(older, null);

        // nothing is deleted without an answer: a queue that must not stop adds the file beside the old one
        if (quiet) return new Arrival(null, name);
        var replace = await AskAsync(new ConfirmVm("NEWER VERSION, OR A FILE OF ITS OWN?",
            $"{name ?? info.FileName} comes from the same Nexus page as {older.ModName} {older.Version}, and Nexus does not say whether it replaces it. Pages often carry files that belong side by side (a core file and its data, optional add-ons, variants). Put it in place of {older.ModName} {older.Version}? The game is updated and the old version is deleted from the library.",
            "REPLACE OLD VERSION", false, [$"{older.ModName} {older.Version}   → {info.Version ?? name ?? "new file"}"]) { CancelLabel = "Add as a separate mod" });
        return replace ? new Arrival(older, null) : new Arrival(null, name);
    }
}
