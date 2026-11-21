using Afterlife.App.Services;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Linking mods added from archives on disk to their page on Nexus Mods, all at once and on import.</summary>
public sealed partial class MainViewModel
{
    private sealed record LinkRun(List<(PackageManifest Package, NexusLinkMatch Match)> Verified, int Unconfirmed, int Unread, bool RateLimited);

    /// <summary>
    /// Asks Nexus about every mod that was added by hand and is not linked yet, shows what it confirmed and links
    /// those after one question. A mod Nexus does not confirm is never linked.
    /// </summary>
    [RelayCommand]
    private Task LinkGuessedMods() => LockedOffline("Linking mods to Nexus Mods") ? Task.CompletedTask : RunUiAsync(LinkGuessedModsAsync);

    private async Task LinkGuessedModsAsync()
    {
        if (_overview is null) return;
        var key = NexusKeyStore.Load();
        if (key is null)
        {
            Say("To link mods to Nexus Mods, paste your personal API key in Settings first.", Tone.Warn);
            OpenSettings();
            return;
        }
        var packages = _overview.Mods.Select(m => m.Package).ToList();
        var candidates = NexusLinker.Candidates(packages);
        var noHint = NexusLinker.WithoutHint(packages).Count;
        if (candidates.Count == 0)
        {
            Say(noHint == 0 ? "Every mod added by hand is linked to Nexus Mods already."
                : $"Nothing to look up: {noHint} mod(s) carry neither a Nexus id in the file name nor a checksum of their archive. Link them one by one in the details.", Tone.Info);
            return;
        }
        var run = await VerifyLinksAsync(candidates, key, showProgress: true);
        var rest = RestLine(run, noHint);
        if (run.Verified.Count == 0)
        {
            Say($"Nexus Mods confirmed none of the {candidates.Count} mod(s).{rest}", run.Unread > 0 ? Tone.Warn : Tone.Info);
            return;
        }
        var lines = run.Verified.OrderBy(v => v.Package.ModName, StringComparer.OrdinalIgnoreCase)
            .Select(v => $"{v.Package.ModName}   · {v.Match.ModName ?? "Nexus mod"} ({v.Match.ModId}) · {v.Match.EvidenceLabel}").ToList();
        var ok = await AskAsync(new ConfirmVm($"LINK {run.Verified.Count} MOD(S) TO NEXUS MODS?",
            $"Nexus Mods confirmed these mods by the content or the name of their archive. Linked mods show their page, requirements and updates.{rest}",
            "LINK", false, lines, null, true, "CONFIRMED BY NEXUS MODS"));
        if (!ok) return;
        var linked = await ApplyLinksAsync(run.Verified.Select(v => v.Match).ToList());
        await RefreshAsync();
        Say($"Linked {linked} mod(s) to Nexus Mods. Reading their pages…", Tone.Info);
        var read = await FetchNexusInfoAsync(onlyMissing: true, limit: int.MaxValue);
        Say(read ? $"Linked {linked} mod(s) to Nexus Mods.{rest}"
                 : $"Linked {linked} mod(s) to Nexus Mods. Some pages could not be read yet; they come with the next start.{rest}", read ? Tone.Ok : Tone.Warn);
    }

    private static string RestLine(LinkRun run, int noHint)
    {
        var parts = new List<string>();
        if (run.Unconfirmed > 0) parts.Add($"{run.Unconfirmed} not confirmed (left as they are)");
        if (run.Unread > 0) parts.Add(run.RateLimited ? $"{run.Unread} not asked yet: the request limit of Nexus Mods was reached, run it again later"
                                                      : $"{run.Unread} could not be asked, Nexus Mods did not answer");
        if (noHint > 0) parts.Add($"{noHint} with nothing to look up");
        return parts.Count == 0 ? "" : " " + string.Join(" · ", parts) + ".";
    }

    /// <summary>
    /// One or two requests per package. Stops at the rate limit; what was not asked stays for the next run, and a
    /// package Nexus confirmed nothing for is not asked again for a day.
    /// </summary>
    private async Task<LinkRun> VerifyLinksAsync(IReadOnlyList<PackageManifest> candidates, string key, bool showProgress)
    {
        var now = DateTimeOffset.UtcNow;
        var tried = await _host.RunAsync(c => NexusLinker.TriedRecently(c.Store, now));
        var verified = new List<(PackageManifest, NexusLinkMatch)>();
        var misses = new List<string>();
        int unconfirmed = 0, unread = 0;
        var limited = false;
        using var client = new NexusClient(key, AppVersion);
        try
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
                if (tried.Contains(p.PackageId)) { unconfirmed++; continue; }
                if (showProgress) { PhaseTitle = $"ASKING NEXUS · {i + 1} / {candidates.Count}"; PhaseTone = Tone.Info; }
                try
                {
                    if (await NexusLinker.VerifyAsync(client, p) is { } match) verified.Add((p, match));
                    else { misses.Add(p.PackageId); unconfirmed++; }
                }
                catch (NexusException ex) when (ex.RateLimited)
                {
                    unread += candidates.Skip(i).Count(x => !tried.Contains(x.PackageId));
                    limited = true;
                    break;
                }
                catch (Exception ex) when (ex is NexusException or System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
                {
                    unread++;
                }
            }
        }
        finally { if (showProgress) PhaseIdle(); }
        if (misses.Count > 0)
            await _host.RunAsync(c => c.Store.InTransaction(() => { foreach (var id in misses) NexusLinker.MarkTried(c.Store, id, now); }));
        return new LinkRun(verified, unconfirmed, unread, limited);
    }

    /// <summary>Writes the links; a package that left the library in the meantime is skipped.</summary>
    private Task<int> ApplyLinksAsync(IReadOnlyList<NexusLinkMatch> matches) => _host.RunAsync(c =>
    {
        var done = 0;
        c.Store.InTransaction(() =>
        {
            foreach (var m in matches)
            {
                if (c.Store.GetPackage(m.PackageId) is null) continue;
                c.LinkToNexus(m.PackageId, m.ModId, m.FileId);
                done++;
            }
        });
        return done;
    });

    /// <summary>
    /// After an import from disk: an archive Nexus confirms is linked at once, then the pages are read. Quiet: it
    /// never asks and never reports, the mod simply shows its page.
    /// </summary>
    private async Task LinkImportedAsync(IReadOnlyList<PackageManifest> imported)
    {
        try
        {
            var key = AppEdition.Offline ? null : NexusKeyStore.Load();
            var candidates = key is null ? [] : NexusLinker.Candidates(imported);
            if (key is not null && candidates.Count > 0)
            {
                var run = await VerifyLinksAsync(candidates, key, showProgress: false);
                if (run.Verified.Count > 0)
                {
                    await ApplyLinksAsync(run.Verified.Select(v => v.Match).ToList());
                    await RefreshAsync();
                }
            }
            await FetchNexusInfoAsync(onlyMissing: true);
        }
        catch (Exception ex) when (IsExpected(ex)) { }
    }
}
