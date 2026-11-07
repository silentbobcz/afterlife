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

/// <summary>Getting mods into the library: nxm links, Nexus downloads, files and folders.</summary>
public sealed partial class MainViewModel
{
    private string? _pendingLink;

    /// <summary>Downloads a Mod Manager Download link into the library and imports it like a dropped archive.</summary>
    private Task HandleNxmAsync(string text) => RunUiAsync(async () =>
    {
        if (CollectionLink.TryParse(text, out var collection)) { await OpenCollectionAsync(collection); return; }
        if (!NxmLink.TryParse(text, out var link)) { Say("This Nexus link could not be read.", Tone.Block); return; }
        if (!link.IsCyberpunk) { Say($"This link is for {link.Game}, not Cyberpunk 2077.", Tone.Block); return; }
        var key = NexusKeyStore.Load();
        if (key is null)
        {
            Say("To download from Nexus Mods, paste your personal API key in Settings first.", Tone.Warn);
            OpenSettings();
            return;
        }
        if (link.Key is null && _nexusAccount is { IsPremium: false })
        {
            Say("Free Nexus accounts download through Mod Manager Download on the website.", Tone.Warn);
            AppLinks.Open(link.ModPage);
            return;
        }
        var got = await DownloadNexusFileAsync(link, key);
        if (got is null) return;
        var (dest, info) = got.Value;
        await ImportDownloadedAsync(link, dest, info);
    });

    /// <summary>Downloads one Nexus file into library/downloads and records it in the history. Null when it failed (message shown).</summary>
    private async Task<(string Dest, NexusFileInfo Info)?> DownloadNexusFileAsync(NxmLink link, string key, Action<double>? onPercent = null)
    {
        using var client = new NexusClient(key, AppVersion);
        NexusFileInfo info;
        try { info = await client.FileInfoAsync(link); }
        catch (NexusException ex) { Say("Nexus Mods: " + ex.Message, Tone.Block); return null; }
        var folder = Path.Combine(AppSettings.ResolveLibrary(null), "downloads");
        var dest = Path.Combine(folder, info.FileName);
        Say($"Downloading {info.ModName ?? info.FileName} from Nexus Mods…", Tone.Info);
        try
        {
            var links = await client.DownloadLinksAsync(link);
            if (links.Count == 0) { Say("Nexus Mods returned no download server.", Tone.Block); return null; }
            var lastShown = -1;
            var progress = new Progress<(long Done, long? Total)>(p =>
            {
                if (p.Total is not > 0) return;
                onPercent?.Invoke(p.Done * 100.0 / p.Total.Value);
                var pct = (int)(p.Done * 100 / p.Total.Value);
                if (pct / 5 == lastShown) return;
                lastShown = pct / 5;
                PhaseTitle = $"DOWNLOADING · {pct} %";
                PhaseTone = Tone.Info;
            });
            await client.DownloadAsync(links[0], dest, progress);
            PhaseIdle();
        }
        catch (NexusException ex) { PhaseIdle(); Say("Nexus Mods: " + ex.Message, Tone.Block); return null; }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or IOException or TaskCanceledException) { PhaseIdle(); Say("Download failed: " + ex.Message, Tone.Block); return null; }
        try
        {
            await _host.RunAsync(c => DownloadHistory.Record(c.Store, new NexusDownload(link.ModId, link.FileId, info.ModName ?? info.FileName,
                info.Version ?? info.ModVersion, info.FileName, DateTimeOffset.Now)));
        }
        catch (Exception ex) when (IsExpected(ex)) { }
        return (dest, info);
    }

    /// <summary>Imports a downloaded Nexus file; a newer file of a mod already in the library replaces it in the profile.</summary>
    private async Task<List<PackageManifest>> ImportDownloadedAsync(NxmLink link, string dest, NexusFileInfo info,
        IReadOnlyList<FomodChoice>? presetChoices = null, bool enable = true, bool quiet = false)
    {
        // a file a collection is waiting for goes to that collection
        if (_collectionWaiting is { } waiting && waiting(link, dest, info)) return [];
        var older = OlderVersionOf(link.ModId, link.FileId);
        var imported = await ImportFilesCoreAsync([dest], new NexusSource(link.ModId, link.FileId, info.ModName ?? older?.ModName, info.Version ?? info.ModVersion, older?.ModId),
            presetChoices ?? older?.FomodChoices, enable, quiet);
        if (older is not null && imported.Count == 1 && imported[0].PackageId != older.PackageId) await ReplaceOlderAsync(older, imported[0], quiet);
        if (imported.Count > 0) UpdateQueueArrived(link); 
        return imported;
    }

    /// <summary>Set while a collection downloads; returns true when it took the file.</summary>
    private Func<NxmLink, string, NexusFileInfo, bool>? _collectionWaiting;

    [RelayCommand]
    private Task AddMod() => RunUiAsync(async () =>
    {
        var d = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add mods",
            Filter = "Mod archives (*.zip;*.7z;*.rar;*.archive)|*.zip;*.7z;*.rar;*.archive|All files (*.*)|*.*",
            Multiselect = true,
        };
        if (d.ShowDialog() != true) return;
        var rest = await ImportUpdatesFromFilesAsync(d.FileNames);
        if (rest.Count > 0) await ImportFilesCoreAsync(rest);
    });

    public Task ImportFilesAsync(IReadOnlyList<string> files) => RunUiAsync(async () =>
    {
        var rest = await ImportUpdatesFromFilesAsync(files);
        if (rest.Count > 0) await ImportFilesCoreAsync(rest);
    });

    private async Task<List<PackageManifest>> ImportFilesCoreAsync(IReadOnlyList<string> files, NexusSource? nexus = null,
        IReadOnlyList<FomodChoice>? presetChoices = null, bool enable = true, bool quiet = false)
    {
        var imported = new List<string>();
        var packages = new List<PackageManifest>();
        var failed = new List<string>();
        foreach (var file in files)
        {
            string? root = null;
            IReadOnlyList<FomodFile>? fomodFiles = null;
            IReadOnlyList<FomodChoice>? fomodChoices = null;
            while (true)
            {
                Say($"Importing {Path.GetFileName(file)}…", Tone.Info);
                var r = await _host.RunAsync(c => c.Import(new ImportRequest(file, ModName: nexus?.ModName, Version: nexus?.Version, ExistingModId: nexus?.ExistingModId,
                    Options: new ImportOptions(root) { FomodFiles = fomodFiles }, NexusModId: nexus?.ModId, NexusFileId: nexus?.FileId)
                    { FomodChoices = fomodChoices }));
                if (r.Succeeded)
                {
                    var p = r.Package!;
                    imported.Add(r.AlreadyInLibrary ? $"{p.ModName} (already in the library, no second copy)" : p.ModName);
                    packages.Add(p);
                    if (!IsModsOff && enable) await _host.RunAsync(c => c.SetEnabled(p.PackageId, true));
                    await RefreshAsync();
                    SelectedMod = Mods.FirstOrDefault(m => m.PackageId == p.PackageId) ?? SelectedMod;
                    break;
                }
                if (r.Analysis.Status == ImportStatus.NeedsFomod && r.Analysis.FomodConfigPath is { } cfgPath)
                {
                    // the mod's own installer
                    FomodConfig cfg;
                    try { cfg = await _host.RunAsync(c => c.ReadFomod(file, cfgPath)); }
                    catch (ImportException ex) { failed.Add($"{Path.GetFileName(file)}: {ex.Message}"); break; }
                    fomodChoices = presetChoices;
                    if (cfg.Steps.Count > 0 && presetChoices is null)
                    {
                        fomodChoices = await AskAsync(new FomodVm(Path.GetFileName(file), cfg, img => InstallCoordinator.ReadFomodImage(file, cfgPath, img)));
                        if (fomodChoices is null) { failed.Add($"{Path.GetFileName(file)}: cancelled"); break; }
                    }
                    fomodFiles = cfg.Resolve(fomodChoices ?? []);
                    continue;
                }
                if (r.Analysis.Status == ImportStatus.NeedsChoice && r.Analysis.RootCandidates.Count > 0)
                {
                    root = await AskAsync(new VariantChoiceVm(Path.GetFileName(file), r.Analysis.RootCandidates, r.Analysis.Warnings));
                    if (root is null) { failed.Add($"{Path.GetFileName(file)}: cancelled"); break; }
                    continue;
                }
                failed.Add($"{Path.GetFileName(file)}: {string.Join("; ", r.Analysis.Problems)}");
                break;
            }
        }
        if (imported.Count > 0)
        {
            await _host.RunAsync(c => c.RefreshRequirements());
            await RefreshAsync();
            _ = FetchNexusInfoAsync(onlyMissing: true);
        }
        // one line per mod, or nothing when there is nothing to say
        var lines = new List<string>();
        if (imported.Count > 0)
            lines.Add(IsModsOff ? $"Added to the library ({imported.Count}) · select a profile to use them:"
                : $"Added to profile {SelectedProfile?.Name} ({imported.Count}) · apply to put them in the game:");
        lines.AddRange(imported.Select(n => "  · " + n));
        if (failed.Count > 0) lines.Add($"Not added ({failed.Count}):");
        lines.AddRange(failed.Select(f => "  · " + f));
        if (lines.Count > 0 && !quiet) SayLines(lines, failed.Count == 0 ? Tone.Ok : imported.Count == 0 ? Tone.Block : Tone.Warn);
        return packages;
    }
}
