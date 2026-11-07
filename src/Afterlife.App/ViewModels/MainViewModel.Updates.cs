using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using Afterlife.App.Services;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Nexus update checks and the update queue.</summary>
public sealed partial class MainViewModel
{
    /// <summary>Reads summary, author and requirements from the Nexus page of every linked mod.</summary>
    private async Task FetchNexusInfoAsync(bool onlyMissing, string? onlyPackage = null)
    {
        var key = NexusKeyStore.Load();
        if (key is null || _overview is null) return;
        var candidates = _overview.Mods.Select(m => m.Package)
            .Where(p => onlyPackage is null || p.PackageId == onlyPackage)
            .Select(NexusInfo.IdOf).Where(x => x.Id is not null && (x.Confirmed || onlyPackage is not null)).Select(x => x.Id!).Distinct().ToList();
        var ids = onlyMissing
            ? await _host.RunAsync(c => candidates.Where(id => NexusInfo.Load(c.Store, id) is not { } i || DateTimeOffset.UtcNow - i.FetchedAt > TimeSpan.FromDays(7)).ToList())
            : candidates;
        if (ids.Count == 0) return;
        using var client = new NexusClient(key, AppVersion);
        var got = 0;
        foreach (var id in ids.Take(40))
        {
            try
            {
                var info = await client.ModInfoAsync(id);
                await _host.RunAsync(c => NexusInfo.Save(c.Store, info));
                got++;
            }
            catch (Exception ex) when (ex is NexusException or System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { }
        }
        if (got == 0) return;
        await _host.RunAsync(c => c.RefreshRequirements());
        await RefreshAsync();
    }

    /// <summary>
    /// Newer files on Nexus at start and every hour. One request to the "updated mods" feed tells which mods changed;
    /// Files are read only for those, for new mods and for mods not checked in 4 weeks. Without the feed: once a day per mod.
    /// </summary>
    private async Task CheckUpdatesAsync(bool force)
    {
        if (_checkingUpdates) return;
        _checkingUpdates = true;
        try { await CheckUpdatesCoreAsync(force); }
        finally { _checkingUpdates = false; }
    }

    private bool _checkingUpdates;

    private System.Windows.Threading.DispatcherTimer? _updateTimer;

    private void StartUpdateTimer()
    {
        if (_updateTimer is not null) return;
        _updateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _updateTimer.Tick += async (_, _) =>
        {
            if (IsBusy) return;
            try { await CheckUpdatesAsync(force: false); }
            catch (Exception ex) when (IsExpected(ex)) { }
        };
        _updateTimer.Start();
    }

    private async Task CheckUpdatesCoreAsync(bool force)
    {
        var key = NexusKeyStore.Load();
        if (key is null || _overview is null) return;
        var now = DateTimeOffset.UtcNow;
        var packages = _overview.Mods.Select(m => m.Package).Where(p => p.LayerOfModId is null && NexusInfo.IdOf(p) is { Id: not null, Confirmed: true }).ToList();
        if (packages.Count == 0) return;
        using var feedClient = new NexusClient(key, AppVersion);
        List<PackageManifest> due;
        if (force) due = packages;
        else
        {
            var items = await _host.RunAsync(c => packages.Select(p => (p.PackageId, ModId: NexusInfo.IdOf(p).Id!, CheckedAt: NexusUpdates.CheckedAt(c.Store, p.PackageId))).ToList());
            var known = items.Where(i => i.CheckedAt is not null).Select(i => i.CheckedAt!.Value).ToList();
            var period = known.Count == 0 ? null : NexusUpdates.FeedPeriod(known.Min(), now);
            IReadOnlyDictionary<string, long>? feed = null;
            if (period is not null)
                try { feed = await feedClient.UpdatedAsync(period); }
                catch (Exception ex) when (ex is NexusException or System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { }
            if (feed is null)
                due = await _host.RunAsync(c => packages.Where(p => !NexusUpdates.IsFresh(c.Store, p.PackageId, now)).ToList());
            else
            {
                var ids = NexusUpdates.DueAfterFeed(items, feed, now).ToHashSet();
                await _host.RunAsync(c => { foreach (var p in packages.Where(p => !ids.Contains(p.PackageId))) NexusUpdates.MarkChecked(c.Store, p.PackageId, now); });
                due = packages.Where(p => ids.Contains(p.PackageId)).ToList();
            }
        }
        if (due.Count == 0) return;
        using var client = new NexusClient(key, AppVersion);
        var found = 0;
        foreach (var group in due.GroupBy(p => NexusInfo.IdOf(p).Id!).Take(60))
        {
            NexusFiles files;
            try { files = await client.FilesAsync(group.Key); }
            catch (Exception ex) when (ex is NexusException or System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { continue; }
            foreach (var p in group)
            {
                var u = NexusUpdates.Find(files, p.NexusFileId, p.Version, now);
                // a newer file of this mod already in the library is not an update to offer again
                if (u is not null && _overview.Mods.Any(m => m.Package.NexusFileId == u.FileId)) u = null;
                await _host.RunAsync(c => NexusUpdates.Save(c.Store, p.PackageId, u, now));
                if (u is not null) found++;
            }
        }
        if (found > 0 || force)
        {
            await RefreshAsync();
            if (force) Say(found == 0 ? "All mods from Nexus are up to date." : $"{found} mod(s) have a newer version on Nexus.", found == 0 ? Tone.Ok : Tone.Info);
        }
    }

    [RelayCommand]
    private Task CheckUpdates() => RunUiAsync(() => CheckUpdatesAsync(force: true));

    /// <summary>Premium downloads the newer file directly; free accounts open its page (Mod Manager Download comes back here).</summary>
    public Task UpdateModAsync(string packageId) => RunUiAsync(async () =>
    {
        var m = _overview?.Mods.FirstOrDefault(x => x.Package.PackageId == packageId);
        if (m?.Update is not { } u) return;
        var key = NexusKeyStore.Load();
        // the account check may not have finished yet; without it a Premium account was sent to the website
        if (key is not null && _nexusAccount is null) await CheckNexusAsync(null);
        if (key is null || _nexusAccount is not { IsPremium: true })
        {
            // straight to the mod-manager download page; one click there and the file lands in AFTERLIFE.
            var viaManager = SafeNxmRegistered();
            AppLinks.Open(viaManager ? u.Url : u.FilesUrl);
            Say(viaManager
                ? $"The download page of {u.Name ?? "the new version"} opened in your browser. Click Slow download there (Nexus lets free accounts download only on the website); the file comes into AFTERLIFE, replaces the old version and goes into the game."
                : "Nexus lets free accounts download only on the website. Turn on Mod Manager Download links in Settings, or drop the downloaded file into this window: AFTERLIFE recognizes it as the update.", Tone.Info);
            return;
        }
        await UpdateOneAsync(u, quiet: false);
    });

    private async Task<bool> UpdateOneAsync(NexusUpdate u, bool quiet)
    {
        var key = NexusKeyStore.Load();
        if (key is null) return false;
        var link = new NxmLink(NxmLink.Cyberpunk, u.ModId, u.FileId, null, null);
        var got = await DownloadNexusFileAsync(link, key);
        if (got is null) return false;
        var done = await ImportDownloadedAsync(link, got.Value.Dest, got.Value.Info, quiet: quiet);
        return done.Count > 0;
    }

    /// <summary>Offered to every account; Premium downloads directly, others page by page through Mod Manager Download.</summary>
    public bool CanUpdateAll => UpdateCount > 1 || _updateQueue is not null;

    public string UpdateAllLabel => _updateQueue is { } q ? $"UPDATES · {q.Count + (_updateWaiting is null ? 0 : 1)} LEFT" : $"UPDATE ALL ({UpdateCount})";

    /// <summary>Pages still to open (free accounts); <see cref="_updateWaiting"/> is the update whose file is awaited.</summary>
    private Queue<(string Name, NexusUpdate Update)>? _updateQueue;

    private (string Name, NexusUpdate Update)? _updateWaiting;

    private int _updateQueueTotal;

    [RelayCommand]
    private Task UpdateAll() => RunUiAsync(async () =>
    {
        if (_overview is null) return;
        if (_updateQueue is not null) { await UpdateQueueMenuAsync(); return; }
        if (NexusKeyStore.Load() is not null && _nexusAccount is null) await CheckNexusAsync(null);
        if (_nexusAccount is not { IsPremium: true }) { await UpdateAllViaWebsiteAsync(); return; }
        var list = _overview.Mods.Where(m => m.Update is not null).Select(m => (m.Package.ModName, Update: m.Update!)).ToList();
        if (list.Count == 0) return;
        var ok = await AskAsync(new ConfirmVm("UPDATE ALL?",
            $"AFTERLIFE downloads {list.Count} newer version(s) from Nexus Mods, puts them in place of the old ones (profiles, collections, categories), updates the game and deletes the old versions from the library.",
            "UPDATE ALL", false, list.Select(x => $"{x.ModName}   → {x.Update.Version ?? x.Update.Name}").ToList()));
        if (!ok) return;
        var failed = new List<string>();
        _updateBatch = true;
        try
        {
            var i = 0;
            foreach (var (name, u) in list)
            {
                Say($"Updating {name} ({++i}/{list.Count})…", Tone.Info);
                if (!await UpdateOneAsync(u, quiet: true)) failed.Add(name);
            }
        }
        finally { _updateBatch = false; }
        await FinishUpdatesAsync(quiet: false);
        if (failed.Count > 0) SayLines([.. NoticeLines, $"Not updated ({failed.Count}):", .. failed.Select(f => "  · " + f)], Tone.Warn);
    });

    /// <summary>Free account: one download page after another; the next opens when the previous file has arrived.</summary>
    private async Task UpdateAllViaWebsiteAsync()
    {
        if (_overview is null) return;
        var list = _overview.Mods.Where(m => m.Update is not null).Select(m => (m.Package.ModName, Update: m.Update!)).ToList();
        if (list.Count == 0) return;
        if (!SafeNxmRegistered())
        {
            Say("Nexus lets free accounts download only on the website. Turn on Mod Manager Download links in Settings first; then UPDATE ALL opens the download pages one after another and the files come straight into AFTERLIFE.", Tone.Warn);
            return;
        }
        var ok = await AskAsync(new ConfirmVm("UPDATE ALL · THROUGH THE WEBSITE",
            $"Nexus lets free accounts download only on the website. AFTERLIFE opens the download page of each of the {list.Count} mods one after another: click Slow download there and the file comes into AFTERLIFE, replaces the old version, goes into the game and the old version is deleted. The next page opens as soon as the previous file has arrived.",
            "OPEN THE FIRST PAGE", false, list.Select(x => $"{x.ModName}   → {x.Update.Version ?? x.Update.Name}").ToList()));
        if (!ok) return;
        _collectionUpdateChoice = null;
        _updateQueue = new Queue<(string, NexusUpdate)>(list);
        _updateQueueTotal = list.Count;
        OpenNextUpdatePage();
    }

    private void OpenNextUpdatePage()
    {
        if (_updateQueue is null) return;
        if (_updateQueue.Count == 0)
        {
            _updateQueue = null;
            _updateWaiting = null;
            OnPropertyChanged(nameof(CanUpdateAll));
            OnPropertyChanged(nameof(UpdateAllLabel));
            Say($"All {_updateQueueTotal} update(s) arrived · apply to put them in the game.", Tone.Ok);
            return;
        }
        var next = _updateQueue.Dequeue();
        _updateWaiting = next;
        OnPropertyChanged(nameof(CanUpdateAll));
        OnPropertyChanged(nameof(UpdateAllLabel));
        AppLinks.Open(next.Update.Url);
        var i = _updateQueueTotal - _updateQueue.Count;
        Say($"Update {i}/{_updateQueueTotal}: {next.Name} → {next.Update.Version ?? next.Update.Name}. Click Slow download on the page that just opened; the next page follows when the file has arrived. (UPDATES in the header: skip or stop.)", Tone.Info);
    }

    /// <summary>Called when a Nexus file arrived through nxm://: if it is the update being waited for, go on with the next page.</summary>
    private void UpdateQueueArrived(NxmLink link)
    {
        if (_updateQueue is null || _updateWaiting is not { } w) return;
        if (w.Update.ModId != link.ModId) return;
        OpenNextUpdatePage();
    }

    private async Task UpdateQueueMenuAsync()
    {
        if (_updateQueue is null) return;
        var left = _updateQueue.Count + (_updateWaiting is null ? 0 : 1);
        var skip = await AskAsync(new ConfirmVm("UPDATES THROUGH THE WEBSITE",
            $"Waiting for {_updateWaiting?.Name ?? "the current download"}; {left} update(s) left. Skip it and open the next page, or stop here (what already arrived stays).",
            "SKIP · NEXT PAGE", false) { CancelLabel = "Stop" });
        if (skip) { OpenNextUpdatePage(); return; }
        _updateQueue = null;
        _updateWaiting = null;
        OnPropertyChanged(nameof(CanUpdateAll));
        OnPropertyChanged(nameof(UpdateAllLabel));
        Say("Updates through the website stopped. What already arrived is in the library.", Tone.Neutral);
    }

    /// <summary>The newest package of this Nexus mod in the library (another file than the one coming in).</summary>
    private PackageManifest? OlderVersionOf(string nexusModId, string? fileId) =>
        _overview?.Mods.Select(m => m.Package)
            .Where(p => p.LayerOfModId is null && NexusInfo.IdOf(p).Id == nexusModId && (fileId is null || p.NexusFileId != fileId))
            .OrderByDescending(p => p.ImportedAt).FirstOrDefault();

    /// <summary>The new version takes the place of the old one in the profile; the old one stays in the library, switched off.</summary>
    /// <summary>
    /// An update replaces the old version completely: the new one takes its place in every profile, collection
    /// And category, the game is updated at once (when the profile is the one in the game) and the old version is
    /// Deleted from the library. In a batch (<see cref="_updateBatch"/>) the apply and the clean-up run once at the end.
    /// </summary>
    private async Task ReplaceOlderAsync(PackageManifest older, PackageManifest newer, bool quiet)
    {
        if (_overview is null) return;
        await _host.RunAsync(c =>
        {
            c.ReplaceVersion(older.PackageId, newer.PackageId);
            NexusUpdates.Save(c.Store, older.PackageId, null, DateTimeOffset.UtcNow);
        });
        // the new version keeps the category of the old one
        if (_layout.CategoryIdOf(older.PackageId) is { } cat) await SaveLayoutAsync(_layout.Assign([newer.PackageId], cat));
        _replacedOld.Add((older.PackageId, $"{older.ModName} {older.Version} → {newer.Version}"));
        await RefreshAsync();
        if (_updateBatch) return;
        await FinishUpdatesAsync(quiet);
    }

    /// <summary>Old versions replaced since the last <see cref="FinishUpdatesAsync"/>.</summary>
    private readonly List<(string OldId, string Line)> _replacedOld = [];

    /// <summary>True while Update all / Replace all runs: apply and clean-up wait for the end.</summary>
    private bool _updateBatch;

    /// <summary>Puts the updates in the game (no extra question) and deletes the old versions from the library.</summary>
    private async Task FinishUpdatesAsync(bool quiet)
    {
        if (_replacedOld.Count == 0 || _overview is null) return;
        var replaced = _replacedOld.ToList();
        _replacedOld.Clear();
        var lines = new List<string>();
        var deployed = _overview.Profiles.FirstOrDefault(p => p.IsSelected) is { IsDeployed: true } && !IsModsOff;
        if (deployed && _overview.PendingChanges > 0)
        {
            await ApplyCoreAsync(PlanDecisions.None);
            await RefreshAsync();
        }
        var stillInGame = _overview?.Mods.Where(m => m.InGame).Select(m => m.Package.PackageId).ToHashSet() ?? [];
        var drop = replaced.Select(r => r.OldId).Where(id => !stillInGame.Contains(id)).Distinct().ToList();
        if (drop.Count > 0)
        {
            var r = await _host.RunAsync(c => c.DeleteFromLibrary(drop));
            await RefreshAsync();
            if (r.Kept.Count > 0) lines.AddRange(r.Kept.Select(k => $"  · {k.Name}: old version kept ({k.Reason})"));
        }
        var waiting = replaced.Count(r => stillInGame.Contains(r.OldId));
        if (quiet) return;
        var head = waiting == 0
            ? (deployed ? "Updated and in the game; the old version is deleted:" : "Updated in the profile; the old version is deleted (apply to put the profile in the game):")
            : "Updated in the profile, but the game could not be changed now (see above). Apply again; the old version is deleted afterwards:";
        lines.InsertRange(0, [head, .. replaced.Select(r => "  · " + r.Line)]);
        SayLines(lines, waiting == 0 ? Tone.Ok : Tone.Warn);
    }

    /// <summary>
    /// A dropped archive named like a Nexus download of a mod already in the library is an update of that mod
    /// (old "Name-4885-0-2-27-1761517000" and new "Name-4885-0.2.27-2026-10-26T22-14Z-xxxx" file names).
    /// </summary>
    private async Task<List<string>> ImportUpdatesFromFilesAsync(IReadOnlyList<string> files)
    {
        var plain = new List<string>();
        var updates = new List<(string File, PackageManifest Older, string NexusModId, string? Version)>();
        foreach (var file in files)
        {
            var guess = PackageLibrary.GuessFromFileName(file);
            var older = guess.NexusModId is null ? null : OlderVersionOf(guess.NexusModId, null);
            if (older is null) { plain.Add(file); continue; }
            updates.Add((file, older, guess.NexusModId!, guess.Version == "unknown" ? null : guess.Version));
        }
        if (updates.Count == 0) return plain;

        // the same Nexus page can also have optional files: ask, do not guess.
        // several files → one list, one answer (replace all, or add all as separate mods), not a question per file.
        bool replaceAll;
        if (updates.Count == 1)
        {
            var (file, older, nexusId, version) = updates[0];
            replaceAll = await AskAsync(new ConfirmVm("NEWER VERSION OF A MOD YOU HAVE?",
                $"{Path.GetFileName(file)} is a download of {older.ModName} (Nexus mod {nexusId}). Put it in place of {older.ModName} {older.Version}? The game is updated and the old version is deleted from the library.",
                "REPLACE OLD VERSION", false, [$"{older.ModName} {older.Version}   → {version ?? "new file"}"]) { CancelLabel = "Add as a separate mod" });
        }
        else
        {
            replaceAll = await AskAsync(new ConfirmVm($"NEWER VERSIONS OF {updates.Count} MODS YOU HAVE?",
                $"{updates.Count} of the {files.Count} files are downloads of mods already in the library. Put each in place of the old version? The game is updated and the old versions are deleted from the library. The other {plain.Count} file(s) are imported as new mods either way.",
                $"REPLACE ALL ({updates.Count})", false,
                updates.Select(u => $"{u.Older.ModName} {u.Older.Version}   → {u.Version ?? "new file"}   ({Path.GetFileName(u.File)})").ToList(),
                linesTitle: "Replaced") { CancelLabel = "Add all as separate mods" });
        }
        if (!replaceAll) { plain.AddRange(updates.Select(u => u.File)); return plain; }

        // all replacements first, then one apply and one clean-up
        _updateBatch = true;
        try
        {
            foreach (var (file, older, nexusId, version) in updates)
            {
                var imported = await ImportFilesCoreAsync([file], new NexusSource(nexusId, null, older.ModName, version, older.ModId), older.FomodChoices, quiet: updates.Count > 1);
                if (imported.Count == 1 && imported[0].PackageId != older.PackageId)
                    await ReplaceOlderAsync(older, imported[0], quiet: true);
            }
        }
        finally { _updateBatch = false; }
        await FinishUpdatesAsync(quiet: false);
        return plain;
    }
}
