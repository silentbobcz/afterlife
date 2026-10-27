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

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly CoordinatorHost _host = new();
    private InstallOverview? _overview;
    private bool _building;

    public MainViewModel()
    {
        ModsView = CollectionViewSource.GetDefaultView(Mods);
        ModsView.Filter = o => o is ModRowVm r && r.Matches(SearchText.Trim());
        ModsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ModRowVm.Chapter)));
    }

    // ---------------- bindable state ----------------
    public ObservableList<ProfileVm> Profiles { get; } = new();
    public ObservableList<ModRowVm> Mods { get; } = new();
    public ICollectionView ModsView { get; }
    public ObservableList<FrameworkStatus> Frameworks { get; } = new();
    /// <summary>P12: core table in the header (installed and missing base mods).</summary>
    public ObservableList<CoreModVm> CoreMods { get; } = new();
    /// <summary>P27: folders the scan looks at, with the number of files AFTERLIFE did not install.</summary>
    [ObservableProperty] private IReadOnlyList<Controls.ScanFolder> _scanFolders = ScanFoldersOf([]);

    public static readonly (string Prefix, string Label, string Short)[] ScanBuckets =
    {
        ("archive/pc/mod/", "archive/pc/mod", "ARC"),
        ("r6/scripts/", "r6/scripts", "REDS"),
        ("r6/tweaks/", "r6/tweaks", "TWK"),
        ("bin/x64/plugins/", "bin/x64/plugins", "CET"),
        ("red4ext/", "red4ext", "R4X"),
        ("mods/", "mods (REDmod)", "RED"),
        ("", "other paths", "ETC"),
    };

    public static IReadOnlyList<Controls.ScanFolder> ScanFoldersOf(IEnumerable<string> paths)
    {
        var counts = new int[ScanBuckets.Length];
        foreach (var p in paths)
        {
            var i = Array.FindIndex(ScanBuckets, b => b.Prefix.Length > 0 && p.StartsWith(b.Prefix, StringComparison.OrdinalIgnoreCase));
            counts[i < 0 ? ScanBuckets.Length - 1 : i]++;
        }
        return ScanBuckets.Select((b, i) => new Controls.ScanFolder(b.Label, b.Short, counts[i])).ToList();
    }
    /// <summary>P18: notices are a list, one thing per line.</summary>
    public ObservableCollection<string> NoticeLines { get; } = new();

    [ObservableProperty] private OverlayVm? _overlay;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isOperating;
    [ObservableProperty] private string? _notice;
    [ObservableProperty] private Tone _noticeTone;

    [ObservableProperty] private ProfileVm? _selectedProfile;
    [ObservableProperty] private ModRowVm? _selectedMod;
    [ObservableProperty] private DetailsVm? _details;
    [ObservableProperty] private string _searchText = "";

    [ObservableProperty] private string _gameBuild = "–";
    [ObservableProperty] private string? _gameVersion;
    [ObservableProperty] private int _detectedCount;
    [ObservableProperty] private bool _isDragOver;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private bool _hasMultiSelection;

    // breach panel (P9)
    [ObservableProperty] private bool _showBreach;
    [ObservableProperty] private bool _breachActive;
    [ObservableProperty] private string _breachTitle = "";
    [ObservableProperty] private Tone _breachTone = Tone.Info;
    [ObservableProperty] private int _breachDone;
    [ObservableProperty] private int _breachTotal;
    public ObservableCollection<LineVm> BreachLines { get; } = new();
    private int _breachRun;

    /// <summary>Raised after a scan that the user should notice (start, rescan, apply): header sweep + check-off.</summary>
    public event Action? ScanCompleted;
    private IReadOnlyList<ModRowVm> _multi = [];
    [ObservableProperty] private string _gameFolder = "";
    [ObservableProperty] private bool _buildChanged;
    [ObservableProperty] private string _buildBannerTitle = "";
    [ObservableProperty] private bool _recoveryRequired;
    [ObservableProperty] private string _recoveryText = "";
    [ObservableProperty] private string _librarySubtitle = "";
    [ObservableProperty] private string _changesSummary = "";
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private bool _canApply;
    [ObservableProperty] private string _applyBlockedReason = "";
    [ObservableProperty] private bool _canLaunch;
    [ObservableProperty] private string _launchHint = "";
    [ObservableProperty] private int _inboxCount;
    [ObservableProperty] private bool _isModsOff;
    [ObservableProperty] private bool _isEmptyLibrary = true;
    [ObservableProperty] private bool _orderIsCustom;
    [ObservableProperty] private string _nexusBadge = "NEXUS · no key";
    [ObservableProperty] private Tone _nexusTone = Tone.Neutral;
    private NexusAccount? _nexusAccount;

    // operation progress (footer)
    [ObservableProperty] private string _phaseTitle = "READY";
    [ObservableProperty] private Tone _phaseTone = Tone.Neutral;
    [ObservableProperty] private Tone _seg1 = Tone.Neutral;
    [ObservableProperty] private Tone _seg2 = Tone.Neutral;
    [ObservableProperty] private Tone _seg3 = Tone.Neutral;
    [ObservableProperty] private string _seg2Label = "Applying";

    partial void OnSearchTextChanged(string value) { UpdateGroupNames(); ModsView.Refresh(); BuildCollectionChapters(); }

    partial void OnSelectedModChanged(ModRowVm? value)
    {
        if (_building) return;
        Details = value is null ? null : new DetailsVm(value.Overview, this);
        if (Details is not null) _ = LoadImageAsync(Details, value!.Overview);
    }

    partial void OnSelectedProfileChanged(ProfileVm? value)
    {
        OnPropertyChanged(nameof(CanDeleteProfile));
        if (_building || value is null) return;
        _ = RunUiAsync(async () =>
        {
            await _host.RunAsync(c => c.SelectProfile(value.Id));
            await RefreshAsync();
        });
    }

    // ---------------- startup ----------------
    public async Task StartAsync()
    {
        Controls.Fx.SetReduceMotion(AppSettings.Load().ReduceMotion);
        var lib = AppSettings.ResolveLibrary(null);
        if (!AppSettings.IsInitialized(lib))
        {
            Overlay = new FirstRunVm(InitializeAsync);
            return;
        }
        await RunUiAsync(async () =>
        {
            await _host.OpenAsync(lib);
            await RefreshAsync();
            ScanCompleted?.Invoke();
        });
        _ = AfterOpenAsync();
        if (_pendingLink is not null) { var l = _pendingLink; _pendingLink = null; await HandleNxmAsync(l); }
    }

    private string? _pendingLink;

    /// <summary>After opening: new requirement rules for old mods (P40), Nexus account and mod pages (P43).</summary>
    private async Task AfterOpenAsync()
    {
        try
        {
            if (await _host.RunAsync(c => c.RefreshRequirements()) > 0) await RefreshAsync();
        }
        catch (Exception ex) when (IsExpected(ex)) { }
        await CheckDuplicatesAsync(); // P99
        await CheckNexusAsync(null);
        await FetchNexusInfoAsync(onlyMissing: true);
        await CheckUpdatesAsync(force: false);
        StartUpdateTimer();
    }

    /// <summary>P43: reads summary, author and requirements from the Nexus page of every linked mod.</summary>
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
    /// M4: newer files on Nexus at start and every hour. One request to the "updated mods" feed tells which mods changed;
    /// files are read only for those, for new mods and for mods not checked in 4 weeks. Without the feed: once a day per mod.
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

    /// <summary>M4: Premium downloads the newer file directly; free accounts open its page (Mod Manager Download comes back here).</summary>
    public Task UpdateModAsync(string packageId) => RunUiAsync(async () =>
    {
        var m = _overview?.Mods.FirstOrDefault(x => x.Package.PackageId == packageId);
        if (m?.Update is not { } u) return;
        var key = NexusKeyStore.Load();
        // P61: the account check may not have finished yet; without it a Premium account was sent to the website
        if (key is not null && _nexusAccount is null) await CheckNexusAsync(null);
        if (key is null || _nexusAccount is not { IsPremium: true })
        {
            AppLinks.Open(u.Url);
            Say(SafeNxmRegistered()
                ? "Nexus lets free accounts download only on the website: click Mod Manager Download there and AFTERLIFE replaces the old version in the profile."
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

    // ---------------- P59: update all (Premium) ----------------
    [ObservableProperty] private int _updateCount;
    public bool CanUpdateAll => UpdateCount > 1 && _nexusAccount is { IsPremium: true };
    public string UpdateAllLabel => $"UPDATE ALL ({UpdateCount})";

    [RelayCommand]
    private Task UpdateAll() => RunUiAsync(async () =>
    {
        if (_overview is null || _nexusAccount is not { IsPremium: true }) return;
        var list = _overview.Mods.Where(m => m.Update is not null).Select(m => (m.Package.ModName, Update: m.Update!)).ToList();
        if (list.Count == 0) return;
        var ok = await AskAsync(new ConfirmVm("UPDATE ALL?",
            $"AFTERLIFE downloads {list.Count} newer version(s) from Nexus Mods and puts them in the profile instead of the old ones. The old versions stay in the library. Nothing changes in the game until you apply.",
            "UPDATE ALL", false, list.Select(x => $"{x.ModName}   → {x.Update.Version ?? x.Update.Name}").ToList()));
        if (!ok) return;
        _collectionUpdateChoice = null; // P98: asked once, "for all" remembered for this run
        var done = new List<string>();
        var failed = new List<string>();
        foreach (var (name, u) in list)
        {
            Say($"Updating {name} ({done.Count + failed.Count + 1}/{list.Count})…", Tone.Info);
            if (await UpdateOneAsync(u, quiet: true)) done.Add($"{name} → {u.Version ?? u.Name}"); else failed.Add(name);
        }
        await RefreshAsync();
        var lines = new List<string> { $"Updated {done.Count} of {list.Count} · apply to put them in the game:" };
        lines.AddRange(done.Select(d => "  · " + d));
        if (failed.Count > 0) { lines.Add($"Not updated ({failed.Count}):"); lines.AddRange(failed.Select(f => "  · " + f)); }
        SayLines(lines, failed.Count == 0 ? Tone.Ok : Tone.Warn);
    });

    // ---------------- P64: refresh ----------------
    [RelayCommand]
    private Task RefreshLibrary() => RunUiAsync(async () =>
    {
        Say("Refreshing: reading the game folder and the library again…", Tone.Info);
        try { await _host.RunAsync(c => c.RefreshRequirements()); }
        catch (Exception ex) when (IsExpected(ex)) { }
        await RefreshAsync();
        ScanCompleted?.Invoke();
        await FetchNexusInfoAsync(onlyMissing: true);
        await CheckUpdatesAsync(force: false);
        Say("Refreshed. The list shows the game folder and the library as they are now.", Tone.Ok);
    });

    /// <summary>P41/P43: links a manual import to its Nexus page by id or link.</summary>
    public Task LinkToNexusAsync(string packageId, string? guess) => RunUiAsync(async () =>
    {
        var text = await AskAsync(new TextPromptVm("LINK TO NEXUS", "Nexus mod link or id (for example 107)", guess ?? ""));
        if (text is null) return;
        var m = System.Text.RegularExpressions.Regex.Match(text, @"(?:mods/)?(\d+)\s*/?\s*$|mods/(\d+)");
        var id = m.Success ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) : "";
        await _host.RunAsync(c => c.LinkToNexus(packageId, id));
        await RefreshAsync();
        if (NexusKeyStore.Load() is null) { Say("Linked. Add your Nexus key in Settings to show its description and requirements.", Tone.Info); return; }
        Say($"Linked to Nexus mod {id}. Reading its page…", Tone.Info);
        await FetchNexusInfoAsync(onlyMissing: false, onlyPackage: packageId);
        Say($"Linked to Nexus mod {id}.", Tone.Ok);
    });

    // ---------------- export (P52) ----------------
    [RelayCommand]
    private void ExportList()
    {
        if (_overview is null) return;
        var o = _overview;
        Overlay = new ExportVm(
            (scope, off) => ModListExport.Text(o, scope, off, AppVersion, DateTimeOffset.Now),
            (scope, off) => ModListExport.Csv(o, scope, off),
            CopyToClipboard, HasChanges)
        { CloseCommand = new RelayCommand(() => Overlay = null) };
    }

    private void CopyDiagnostics()
    {
        if (_overview is null) { ShowToast("Open a game install first."); return; }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(ModListExport.Text(_overview, ExportScope.InGameNow, true, AppVersion, DateTimeOffset.Now));
        sb.AppendLine($"Windows {Environment.OSVersion.Version} · .NET {Environment.Version} · {System.Globalization.CultureInfo.CurrentUICulture.Name}");
        sb.AppendLine($"Unfinished change: {(_overview.Status.Unfinished is null ? "no" : "yes")} · library files in game: {_overview.Status.LedgerFiles}");
        try
        {
            var log = Directory.Exists(App.LogDirectory) ? Directory.GetFiles(App.LogDirectory, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
            if (log is not null)
            {
                var lines = File.ReadAllLines(log).Where(l => l.Contains("ERR", StringComparison.OrdinalIgnoreCase) || l.Contains("WARN", StringComparison.OrdinalIgnoreCase)).TakeLast(30).ToList();
                if (lines.Count > 0) { sb.AppendLine("LAST WARNINGS AND ERRORS"); foreach (var l in lines) sb.AppendLine("  " + l); }
            }
        }
        catch (IOException) { }
        CopyToClipboard(sb.ToString().Replace(_overview.Status.GameRoot, "<game>", StringComparison.OrdinalIgnoreCase)
            .Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase));
    }

    // ---------------- storage (P57) ----------------
    private async Task LoadStorageAsync(SettingsVm vm)
    {
        if (!_host.IsOpen) return;
        try { vm.SetStorage(await _host.RunAsync(c => c.StorageInfo())); }
        catch (Exception ex) when (IsExpected(ex)) { }
    }

    private async Task PurgeAsync(SettingsVm vm, string kind)
    {
        if (!_host.IsOpen) return;
        var (removed, downloads, temp) = kind switch
        {
            "removed" => (true, false, false), "downloads" => (false, true, false), "temp" => (false, false, true), _ => (true, true, true),
        };
        var lines = new List<string>();
        if (removed) lines.Add($"Removed files ({vm.RemovedSize}): mods, manual mods and leftovers you took out. They can no longer be restored.");
        if (downloads) lines.Add($"Downloaded archives ({vm.DownloadSize}): Import again in Download history stops working; the mods in your library stay.");
        if (temp) lines.Add($"Temporary files ({vm.TempSize}): unpacked imports that did not finish.");
        var ok = await AskAsync(new ConfirmVm("DELETE KEPT FILES?",
            "These files are deleted for good. Your game, the mods in the library and your profiles are not touched.",
            "DELETE", danger: true, lines, null, true, "WHAT GOES"));
        if (!ok) return;
        try
        {
            var freed = await _host.RunAsync(c => c.PurgeStorage(removed, downloads, temp));
            vm.Message = $"Freed {SettingsVm.Size(freed)}.";
            vm.MessageTone = Tone.Ok;
            await RefreshAsync();
        }
        catch (Exception ex) when (IsExpected(ex)) { vm.Message = ex.Message; vm.MessageTone = Tone.Block; }
        await LoadStorageAsync(vm);
    }

    /// <summary>R9: undoes what AFTERLIFE set up in Windows; the game and the library stay unless chosen.</summary>
    private async Task RemoveFromPcAsync(SettingsVm vm)
    {
        var confirm = new ConfirmVm("REMOVE AFTERLIFE FROM THIS PC?",
            "AFTERLIFE gives Mod Manager Download links back to the previous app, deletes its settings and your saved Nexus key. Then delete AFTERLIFE.exe yourself. "
            + "Mods stay in the game as they are now; to take them out first, switch to Mods off and apply.",
            "REMOVE", danger: true, [$"Library: {AppSettings.ResolveLibrary(null)}"], "Also delete the library (all imported mods and removed files)", false, "KEPT UNLESS CHECKED");
        if (!await AskAsync(confirm)) return;
        try
        {
            if (NxmProtocol.IsRegistered) NxmProtocol.Unregister();
            NexusKeyStore.Clear();
            var lib = AppSettings.ResolveLibrary(null);
            if (confirm.OptionChecked) { _host.Dispose(); try { Directory.Delete(lib, true); } catch (IOException) { } }
            try { Directory.Delete(AppSettings.SettingsDirectory, true); } catch (IOException) { }
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            vm.Message = "Windows refused it: " + ex.Message;
            vm.MessageTone = Tone.Block;
        }
    }

    // ---------------- toast (P47) ----------------
    [ObservableProperty] private string? _toast;
    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    /// <summary>Short notice at the bottom of the window that disappears after 2 seconds.</summary>
    public void ShowToast(string text)
    {
        Toast = text;
        _toastTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _toastTimer.Stop();
        _toastTimer.Tick -= ToastTick;
        _toastTimer.Tick += ToastTick;
        _toastTimer.Start();
    }

    private void ToastTick(object? sender, EventArgs e) { _toastTimer?.Stop(); Toast = null; }

    public void CopyToClipboard(string text)
    {
        try { System.Windows.Clipboard.SetText(text); ShowToast($"Copied to clipboard: {text}"); }
        catch (System.Runtime.InteropServices.ExternalException) { ShowToast("The clipboard is busy; try again."); }
    }

    // ---------------- download history (P48) ----------------
    [RelayCommand]
    private Task OpenDownloads() => RunUiAsync(async () =>
    {
        var (list, packages) = _host.IsOpen
            ? await _host.RunAsync(c => (DownloadHistory.List(c.Store), c.Store.GetPackages()))
            : ((IReadOnlyList<NexusDownload>)[], (IReadOnlyList<PackageManifest>)[]);
        var folder = Path.Combine(AppSettings.ResolveLibrary(null), "downloads");
        var rows = list.Select(d => (d.ModId, d.FileId, d.Name, d.Version, d.FileName, (DateTimeOffset?)d.DownloadedAt, d.Url)).ToList();
        // mods downloaded before the history existed
        foreach (var p in packages.Where(p => p.Origin == PackageOrigin.Nexus && p.NexusModId is not null))
            if (!rows.Any(r => r.ModId == p.NexusModId && (p.NexusFileId is null || r.FileId == p.NexusFileId)))
                rows.Add((p.NexusModId!, p.NexusFileId ?? "", p.ModName, p.Version, p.SourceArchiveName ?? "", p.ImportedAt, $"https://www.nexusmods.com/{NxmLink.Cyberpunk}/mods/{p.NexusModId}"));
        var items = rows.OrderByDescending(r => r.Item6 ?? DateTimeOffset.MinValue).Select(r =>
        {
            var inLibrary = packages.Any(p => p.NexusModId == r.ModId && (r.FileId == "" || p.NexusFileId is null || p.NexusFileId == r.FileId));
            var file = r.FileName.Length == 0 ? null : Path.Combine(folder, r.FileName);
            var canReimport = !inLibrary && file is not null && File.Exists(file);
            var url = r.Url;
            return new DownloadVm(r.Name, $"{(r.Version is null ? "" : "v" + r.Version + " · ")}{(r.Item6 is { } at ? at.LocalDateTime.ToString("g") : "date unknown")}",
                inLibrary ? "in library" : canReimport ? "deleted · file kept" : "deleted",
                inLibrary ? Tone.Ok : Tone.Neutral,
                new RelayCommand(() => AppLinks.Open(url)),
                canReimport ? new AsyncRelayCommand(async () => { Overlay = null; await ImportFilesCoreAsync([file!], new NexusSource(r.ModId, r.FileId, r.Name, r.Version)); }) : null);
        }).ToList();
        Overlay = new DownloadsVm(items) { CloseCommand = new RelayCommand(() => Overlay = null) };
    });

    /// <summary>P39: the detail tab the user picked; kept while switching between mods.</summary>
    public DetailTab? PreferredTab { get; set; }

    /// <summary>nxm:// link from the command line or from a second AFTERLIFE start (P24).</summary>
    public Task HandleExternalAsync(string message)
    {
        if (!message.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase)) return Task.CompletedTask;
        if (!_host.IsOpen) { _pendingLink = message; return Task.CompletedTask; }
        return HandleNxmAsync(message);
    }

    private async Task InitializeAsync(FirstRunVm vm)
    {
        try
        {
            var lib = Path.GetFullPath(string.IsNullOrWhiteSpace(vm.LibraryFolder) ? AppSettings.DefaultLibraryRoot : vm.LibraryFolder);
            if (AppSettings.IsInitialized(lib)) await _host.OpenAsync(lib);
            else await _host.InitializeAsync(vm.GameFolder, lib);
            new AppSettings(lib).Save();
            Overlay = null;
            await RefreshAsync();
            ScanCompleted?.Invoke();
            if (!string.IsNullOrWhiteSpace(vm.NexusKey))
            {
                try
                {
                    using var client = new NexusClient(vm.NexusKey.Trim(), AppVersion);
                    var acc = await client.ValidateAsync();
                    NexusKeyStore.Save(vm.NexusKey.Trim());
                    SetAccount(acc, null);
                }
                catch (NexusException ex) { Say("The Nexus key was not saved: " + ex.Message + ". Add it later in Settings.", Tone.Warn); return; }
            }
            _ = AfterOpenAsync();
            Say(DetectedCount > 0
                ? $"Ready. Found {DetectedCount} mod(s) installed outside AFTERLIFE; they are listed in Scan manual mods and left untouched."
                : "Ready. Existing files in the game were scanned and left untouched.", Tone.Ok);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            vm.Warning = ex.Message;
        }
    }

    // ---------------- refresh ----------------
    public async Task RefreshAsync()
    {
        if (!_host.IsOpen) return;
        var o = await _host.RunAsync(OverviewBuilder.Build);
        await LoadLayoutAsync(); // P95/P97: collections and the block layout decide the groups
        Apply(o);
        BuildCollectionChapters();
        PruneLayout();
    }

    private void Apply(InstallOverview o)
    {
        _overview = o;
        _building = true;
        try
        {
            var st = o.Status;
            GameFolder = st.GameRoot;
            GameBuild = st.Fingerprint?.FileVersion ?? "not found";
            GameVersion = st.Fingerprint?.GameVersion;
            BuildChanged = st.BuildChanged;
            BuildBannerTitle = $"GAME UPDATED TO {st.Fingerprint?.GameVersion ?? st.Fingerprint?.FileVersion ?? "?"}";
            RecoveryRequired = st.Unfinished is not null;
            RecoveryText = st.Unfinished is null ? "" :
                $"The last change to the game did not finish ({st.Unfinished.Status}, {st.Unfinished.CreatedAt.LocalDateTime:g}). Finish it, or return to the previous working state. Unknown files are never deleted.";
            IsModsOff = st.Selected.IsSystemModsOff;

            Profiles.Reset(o.Profiles.Select(p => new ProfileVm(p,
                p.IsSystem ? "no managed mods in game" : $"{p.Wanted} mod{(p.Wanted == 1 ? "" : "s")} wanted",
                // P67: a deployed profile with changes not applied yet is not "deployed" in the green sense
                p.IsDeployed && p.Id == st.Selected.Id && o.PendingChanges > 0 ? "PENDING" : p.IsDeployed ? "DEPLOYED" : (p.IsSystem ? "SYSTEM" : null),
                p.IsDeployed && p.Id == st.Selected.Id && o.PendingChanges > 0 ? Tone.Info : p.IsDeployed ? Tone.Ok : Tone.Neutral)));
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == st.Selected.Id);

            var keep = SelectedMod?.PackageId;
            var keepMulti = Mods.Where(m => m.IsChecked).Select(m => m.PackageId).ToHashSet();
            var rows = o.Mods.Select(m => new ModRowVm(m, !st.Selected.IsSystemModsOff && st.Unfinished is null, OnWantChangedAsync)).ToList();
            AssignChapters(rows);
            MarkTwins(rows, o);
            Mods.Reset(rows.OrderBy(r => r.BlockRank)); // stable: inside a block the load order stays
            UpdateGroupNames();
            foreach (var m in Mods)
            {
                m.IsChecked = keepMulti.Contains(m.PackageId);
                m.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ModRowVm.IsChecked)) UpdateChecked(); };
            }
            IsEmptyLibrary = Mods.Count == 0;
            UpdateCount = o.Mods.Count(m => m.Update is not null);
            OnPropertyChanged(nameof(CanUpdateAll));
            OnPropertyChanged(nameof(UpdateAllLabel));
            SelectedMod = Mods.FirstOrDefault(m => m.PackageId == keep) ?? Mods.FirstOrDefault();
            Details = SelectedMod is null ? null : new DetailsVm(SelectedMod.Overview, this);
            if (Details is not null) _ = LoadImageAsync(Details, SelectedMod!.Overview);
            UpdateChecked();

            var wanted = o.Mods.Count(m => m.Wanted);
            var deployed = o.Mods.Count(m => m.InGame);
            LibrarySubtitle = $"{o.Mods.Count} mod{(o.Mods.Count == 1 ? "" : "s")} · {wanted} in profile {st.Selected.Name} · {deployed} in game"
                + (o.OrderIsCustom ? " · custom load order" : "");
            Frameworks.Reset(o.Frameworks);
            CoreMods.Reset(o.CoreMods.Select(c => new CoreModVm(c)));
            OrderIsCustom = o.OrderIsCustom;
            InboxCount = o.Unmanaged.Count;
            ScanFolders = ScanFoldersOf(o.Unmanaged.Select(u => u.Path));
            DetectedCount = o.Detected.Count(d => d.Removable);
            if (Overlay is InboxVm inbox) inbox.Update(o);

            var blocking = o.Preview.EnvironmentIssues.Where(i => i.Severity == Severity.Blocking).Concat(o.Preview.Plan.Blockers).ToList();
            var profileSwitch = !o.IsDeployedProfile;
            HasChanges = o.PendingChanges > 0 || profileSwitch;
            ChangesSummary = o.PendingChanges > 0 ? o.ChangesSummary : (profileSwitch ? $"Switch the game to {st.Selected.Name}" : o.ChangesSummary);
            CanApply = HasChanges && blocking.Count == 0 && !IsOperating;
            ApplyBlockedReason = blocking.Count == 0 ? "" : blocking[0].Message + (blocking.Count > 1 ? $" (+{blocking.Count - 1} more)" : "");
            CanLaunch = o.CanLaunch && !IsOperating;
            LaunchHint = o.LaunchHint;
        }
        finally { _building = false; }
        UpdateChecked(); // P67: the count must follow the rebuilt list (deleted mods drop out)
        OnPropertyChanged(nameof(CanDeleteProfile));
    }

    // ---------------- profile and mods ----------------
    private async Task OnWantChangedAsync(ModRowVm row, bool want)
    {
        if (IsOperating) { row.ResetWant(!want); return; }
        try
        {
            var ids = await WithRelatedAsync([row.PackageId], want);
            if (ids is null) { row.ResetWant(!want); return; }
            // P98: another version of the same mod is on: offer to switch it off
            var twinsOff = want ? await OtherVersionsToSwitchOffAsync(row.PackageId) : [];
            if (twinsOff.Count > 0) await _host.RunAsync(c => c.SetEnabledMany(c.SelectedProfile.Id, twinsOff, false));
            if (ids.Count == 1) await _host.RunAsync(c => c.SetEnabled(row.PackageId, want));
            else
            {
                await _host.RunAsync(c => c.SetEnabledMany(c.SelectedProfile.Id, ids, want));
                Say(want ? $"{row.Name} and {ids.Count - 1} mod(s) it needs are in the profile. Apply to put them in the game."
                         : $"{row.Name} and {ids.Count - 1} mod(s) that need it are switched off. Apply to remove them from the game.", Tone.Info);
            }
            await RefreshAsync();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            row.ResetWant(!want);
            Say(ex.Message, Tone.Block);
        }
    }

    public Task SetWantAsync(string packageId, bool want) => RunUiAsync(async () =>
    {
        var ids = await WithRelatedAsync([packageId], want);
        if (ids is null) return;
        await _host.RunAsync(c => c.SetEnabledMany(c.SelectedProfile.Id, ids, want));
        await RefreshAsync();
    });

    /// <summary>
    /// P53: switching on asks to switch on the needed mods that are off. P54: switching off asks to switch off the
    /// switched-on mods that need it. Returns the ids to change, or null when cancelled.
    /// </summary>
    private async Task<IReadOnlyList<string>?> WithRelatedAsync(IReadOnlyList<string> ids, bool on)
    {
        if (_overview is null || IsModsOff) return ids;
        var related = on ? OverviewBuilder.InactiveRequirements(_overview, ids) : OverviewBuilder.ActiveDependents(_overview, ids);
        if (related.Count == 0) return ids;
        var names = _overview.Mods.Where(m => ids.Contains(m.Package.PackageId)).Select(m => m.Package.ModName).ToList();
        var subject = names.Count == 1 ? names[0] : $"{names.Count} mods";
        var lines = on
            ? related.Select(r => $"{r.Name}   · needed by {r.NeededBy}").ToList()
            : related.Select(r => $"{r.Name}   · needs {r.NeededBy}").ToList();
        var vm = on
            ? new ConfirmVm("ALSO SWITCH ON WHAT IT NEEDS?",
                $"{subject} needs {related.Count} mod(s) that are in your library but switched off in this profile. Without them it may not work.",
                "SWITCH ON", false, lines, $"Also switch on the {related.Count} needed mod(s)", true, "NEEDED, NOW OFF")
            : new ConfirmVm("ALSO SWITCH OFF WHAT NEEDS IT?",
                $"{related.Count} switched-on mod(s) need {subject}. Without it they may not work or show errors in game.",
                "SWITCH OFF", false, lines, $"Also switch off the {related.Count} mod(s) that need it", true, "NEED IT, NOW ON");
        if (!await AskAsync(vm)) return null;
        return vm.OptionChecked ? ids.Concat(related.Select(r => r.PackageId)).Distinct().ToList() : ids;
    }

    public Task OverrideRequirementsAsync(string packageId, IReadOnlyList<string> capabilities) => RunUiAsync(async () =>
    {
        var ok = await AskAsync(new ConfirmVm("ENABLE ANYWAY?",
            "The mod will be deployed although a requirement is missing. It may not work and can show errors in game. This is shown as a warning until the requirement is in the profile.",
            "ENABLE ANYWAY"));
        if (!ok) return;
        await _host.RunAsync(c => c.UpdateSelected(p => capabilities.Aggregate(p, (q, cap) => q.WithOverride(packageId, cap))));
        await RefreshAsync();
    });

    /// <summary>P62: this mod wins every conflict it has, with one click.</summary>
    public Task SetWinnersAsync(IReadOnlyList<string> paths, string packageId, string name) => RunUiAsync(async () =>
    {
        await _host.RunAsync(c => c.UpdateSelected(p => paths.Aggregate(p, (acc, path) => acc.WithWinner(GamePath.Parse(path).Key, packageId))));
        await RefreshAsync();
        Say($"{name} now wins {paths.Count} conflicting file(s). Apply to update the game.", Tone.Info);
    });

    public Task SetWinnerAsync(string path, string packageId) => RunUiAsync(async () =>
    {
        await _host.RunAsync(c => c.UpdateSelected(p => p.WithWinner(GamePath.Parse(path).Key, packageId)));
        await RefreshAsync();
        Say("Winner chosen. Apply to update the game.", Tone.Info);
    });

    public Task RemoveFromLibraryAsync(string packageId, string name) => DeleteManyAsync([packageId]);

    /// <summary>
    /// P21: one question, then everything: switched off in every profile, taken out of the game (deck) and deleted
    /// from the library. Core mods also list the mods that need them (P15).
    /// </summary>
    private Task DeleteManyAsync(IReadOnlyCollection<string> ids) => RunUiAsync(async () =>
    {
        if (_overview is null || ids.Count == 0) return;
        var rows = _overview.Mods.Where(m => ids.Contains(m.Package.PackageId)).ToList();
        var inGame = rows.Count(r => r.InGame);
        var profiles = _overview.Profiles.Where(p => !p.IsSystem).Select(p => p.Name).ToList();
        var lines = rows.Select(r => $"{r.Package.ModName}{(r.InGame ? "   · in game" : "")}").ToList();
        var caps = rows.SelectMany(r => r.Package.Provides).Where(c => c.StartsWith("framework:", StringComparison.Ordinal)).ToList();
        var dependents = caps.Count == 0 ? [] : await _host.RunAsync(c => c.DependentsOf(caps, ids));
        var msg = $"{(rows.Count == 1 ? rows[0].Package.ModName : $"{rows.Count} mods")} will be removed from every profile ({string.Join(", ", profiles)}), "
            + (inGame > 0 ? $"taken out of the game ({inGame} in game now) " : "")
            + "and deleted from the AFTERLIFE library. Your original archives on disk are not touched.";
        if (dependents.Count > 0)
        {
            lines.Add("");
            lines.Add("NEEDED BY:");
            lines.AddRange(dependents.Select(d => $"{d.Name}   · {(d.InGame ? "deployed" : string.Join(", ", d.Profiles))}"));
        }
        var vm = new ConfirmVm("DELETE EVERYWHERE?", msg, "DELETE EVERYWHERE", danger: true, lines,
            dependents.Count > 0 ? $"Also switch off the {dependents.Count} mod(s) that need it" : null, true,
            rows.Count == 1 && dependents.Count == 0 ? null : "WHAT HAPPENS");
        if (!await AskAsync(vm)) return;
        if (dependents.Count > 0 && vm.OptionChecked) await _host.RunAsync(c => c.DeactivateEverywhere(dependents.Select(d => d.PackageId).ToList()));

        IsOperating = true;
        var title = rows.Count == 1 ? rows[0].Package.ModName : $"{rows.Count} mods";
        if (inGame > 0) BeginBreach($"PURGE · {title}", rows.Sum(r => r.InstallMap.Count));
        try
        {
            var progress = new Progress<DeployProgress>(p => SetPhase(p.Phase, p.Done, p.Total, p.Path, p.Kind));
            var r = await _host.RunAsync(c => c.DeleteEverywhere(ids, progress));
            if (inGame > 0) EndBreach(r.Kept.Count == 0, r.Kept.Count == 0 ? "removed from game" : "needs attention");
            if (r.Execution is not null && r.Kept.Count == 0) SetPhase(DeployPhase.Done, 0, 0);
            var report = new List<string>();
            if (r.Deleted.Count > 0) report.Add($"Deleted everywhere ({r.Deleted.Count}):");
            report.AddRange(r.Deleted.Select(n => "  · " + n));
            if (r.Kept.Count > 0) report.Add($"Not deleted ({r.Kept.Count}):");
            report.AddRange(r.Kept.Select(k => $"  · {k.Name}: {k.Reason}"));
            if (dependents.Count > 0 && vm.OptionChecked) report.Add($"Switched off {dependents.Count} mod(s) that needed it.");
            if (r.Leftovers > 0) report.Add($"{r.Leftovers} file(s) the deleted mods had created themselves (logs, settings) moved to Removed.");
            if (r.Kept.Count == 0) report.AddRange(await CleanLeftoversAsync(caps));
            SayLines(report, r.Kept.Count > 0 ? Tone.Warn : Tone.Ok);
        }
        catch { if (inGame > 0) EndBreach(false, "stopped"); throw; }
        finally
        {
            IsOperating = false;
            await RefreshAsync();
        }
    });

    // ---------------- multi-select (P11, P19, P22) ----------------
    /// <summary>Selection is the checkboxes only (P22): clicking a row shows its details and keeps the selection.</summary>
    private void UpdateChecked()
    {
        if (_building) return;
        _multi = Mods.Where(m => m.IsChecked).ToList();
        SelectedCount = _multi.Count;
        HasMultiSelection = _multi.Count > 0;
        AllChecked = Mods.Count > 0 && _multi.Count == Mods.Count;
    }

    [ObservableProperty] private bool _allChecked;

    public void SetAllChecked(bool value)
    {
        _building = true;
        try { foreach (var m in Mods) m.IsChecked = value && ModsView.Filter(m); }
        finally { _building = false; }
        UpdateChecked();
    }

    [RelayCommand]
    private Task BulkActivate() => RunUiAsync(async () =>
    {
        IReadOnlyList<string>? ids = _multi.Select(m => m.PackageId).ToList();
        if (ids.Count == 0 || _overview is null) return;
        ids = await WithRelatedAsync(ids, true);
        if (ids is null) return;
        await _host.RunAsync(c => c.SetEnabledMany(_overview.Status.Selected.Id, ids, true));
        await RefreshAsync();
        Say($"{ids.Count} mods in profile {_overview.Status.Selected.Name}. Apply to put them in the game.", Tone.Info);
    });

    [RelayCommand]
    private Task BulkDeactivate() => RunUiAsync(async () =>
    {
        IReadOnlyList<string>? ids = _multi.Select(m => m.PackageId).ToList();
        if (ids.Count == 0 || _overview is null) return;
        ids = await WithRelatedAsync(ids, false);
        if (ids is null) return;
        await _host.RunAsync(c => c.SetEnabledMany(_overview.Status.Selected.Id, ids, false));
        await RefreshAsync();
        Say($"{ids.Count} mods switched off in {_overview.Status.Selected.Name}. Apply to remove them from the game.", Tone.Info);
    });

    [RelayCommand]
    private Task BulkDelete()
    {
        var ids = _multi.Select(m => m.PackageId).ToList();
        return ids.Count == 0 ? Task.CompletedTask : DeleteManyAsync(ids);
    }

    [RelayCommand]
    private void ClearSelection() => SetAllChecked(false);

    // ---------------- load order (P25) ----------------
    [RelayCommand]
    private Task ResetOrder() => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff) return;
        await _host.RunAsync(c => c.ResetOrder(_overview.Status.Selected.Id));
        await RefreshAsync();
        Say("Back to the game's default order (alphabetical). Apply to update the game.", Tone.Info);
    });

    // ---------------- inbox (P6) ----------------
    public Task RescanAsync() => RunUiAsync(async () =>
    {
        await RefreshAsync();
        ScanCompleted?.Invoke();
    });

    public Task RemoveDetectedAsync(IReadOnlyList<DetectedMod> mods) => RunUiAsync(async () =>
    {
        if (mods.Count == 0) return;
        var files = mods.SelectMany(m => m.Files.Select(f => f.Path)).ToList();
        var label = mods.Count == 1 ? mods[0].Name : $"{mods.Count} mods";
        var lines = mods.Select(m => $"{m.Name}   · {m.Files.Count} file(s){(m.IsFramework ? " · core mod" : "")}").ToList();
        var caps = mods.Select(m => m.Capability).OfType<string>().ToList();
        var dependents = caps.Count == 0 ? [] : await _host.RunAsync(c => c.DependentsOf(caps));
        if (caps.Count > 0)
        {
            lines.Add("");
            lines.Add(dependents.Count == 0 ? "No AFTERLIFE mod needs these core mods. Manually installed mods may still need them." : "NEEDED BY:");
            lines.AddRange(dependents.Select(d => $"{d.Name}   · {(d.InGame ? "deployed" : string.Join(", ", d.Profiles))}"));
        }
        var vm = new ConfirmVm("REMOVE FROM GAME?",
            $"{label} ({files.Count} files) will be moved out of the game into the AFTERLIFE library (folder \"removed\"). Nothing is deleted; you can restore it at any time from Scan manual mods.",
            "REMOVE FROM GAME", danger: true, lines,
            dependents.Count > 0 ? $"Also switch off the {dependents.Count} mod(s) that need it" : null, true, "WHAT HAPPENS");
        if (!await AskAsync(vm)) return;
        if (dependents.Count > 0 && vm.OptionChecked)
        {
            await _host.RunAsync(c => c.DeactivateEverywhere(dependents.Select(d => d.PackageId).ToList()));
            if (dependents.Any(d => d.InGame)) Say("The mods that need it are switched off in every profile. Apply to take them out of the game.", Tone.Info);
        }
        Overlay = null; // show the breach panel; Inbox can be reopened
        BeginBreach($"PURGE · {label}", files.Count);
        try
        {
            var r = await _host.RunAsync(c => c.RemoveUnmanaged(files, label));
            foreach (var f in files.Take(9)) AddBreachLine($"> PURGE   {f}", Tone.Warn);
            EndBreach(r.Skipped.Count == 0, $"{r.Moved} file(s) moved out");
            await RefreshAsync();
            Say(r.Skipped.Count == 0
                ? $"Removed {label} from the game. Restore it from Scan manual mods if you need it back."
                : $"Removed {r.Moved} file(s); {r.Skipped.Count} changed meanwhile and stayed in the game.", r.Skipped.Count == 0 ? Tone.Ok : Tone.Warn);
        }
        catch { EndBreach(false, "stopped"); throw; }
    });

    /// <summary>
    /// P30 REPLACE: the files in the way are moved into the library ("removed", restorable in Scan manual mods);
    /// then the usual apply question installs the mod.
    /// </summary>
    public Task ReplaceForeignAsync(string packageId, string name, IReadOnlyList<string> paths) => RunUiAsync(async () =>
    {
        var vm = new ConfirmVm("REPLACE FILES IN THE GAME?",
            $"These files are in the game but were not written by AFTERLIFE. They will be moved into the library (folder \"removed\") and {name} takes their place. You can put them back any time in Scan manual mods.",
            "REPLACE", danger: true, paths, linesTitle: "MOVED ASIDE");
        if (!await AskAsync(vm)) return;
        BeginBreach($"PURGE · in the way of {name}", paths.Count);
        try
        {
            var r = await _host.RunAsync(c => c.MoveAsideFor(packageId));
            foreach (var f in paths.Take(9)) AddBreachLine($"> PURGE   {f}", Tone.Warn);
            EndBreach(r.Skipped.Count == 0, $"{r.Moved} file(s) moved aside");
            await RefreshAsync();
            var lines = new List<string> { $"Moved aside {r.Moved} file(s) for {name} (restorable in Scan manual mods)." };
            if (r.Skipped.Count > 0) lines.Add($"Skipped {r.Skipped.Count}: {string.Join(", ", r.Skipped.Take(3))}");
            lines.Add(_overview?.Mods.FirstOrDefault(m => m.Package.PackageId == packageId)?.Wanted == true
                ? "Apply to install it." : $"Switch {name} on and apply to install it.");
            SayLines(lines, r.Skipped.Count == 0 ? Tone.Ok : Tone.Warn);
        }
        catch { EndBreach(false, "stopped"); throw; }
    });

    public Task RestoreRemovedAsync(RemovedBatch batch) => RunUiAsync(async () =>
    {
        Overlay = null;
        BeginBreach($"RESTORE · {batch.Label}", batch.Pending);
        try
        {
            var r = await _host.RunAsync(c => c.RestoreRemoved(batch.Id));
            foreach (var f in batch.Files.Take(9)) AddBreachLine($"> RESTORE {f.Path}", Tone.Info);
            EndBreach(r.Skipped.Count == 0, $"{r.Moved} file(s) restored");
            await RefreshAsync();
            Say(r.Skipped.Count == 0 ? $"{batch.Label} is back in the game."
                : $"Restored {r.Moved} file(s). Skipped {r.Skipped.Count} because a file with the same name is in the game now: {string.Join(", ", r.Skipped.Take(3))}.",
                r.Skipped.Count == 0 ? Tone.Ok : Tone.Warn);
        }
        catch { EndBreach(false, "stopped"); throw; }
    });

    // ---------------- settings (P7) ----------------
    [RelayCommand]
    private void OpenSettings()
    {
        var vm = new SettingsVm(GameFolder, GameVersion is null ? GameBuild : $"{GameVersion} · build {GameBuild}", AppSettings.ResolveLibrary(null),
            AppSettings.Load().ReduceMotion, NexusKeyStore.Load() is not null, SafeNxmRegistered(), SafeOtherHandler(),
            new SettingsActions(ChangeGameFolderAsync, SetReduceMotion, SaveNexusKeyAsync, ForgetNexusKey, CheckNexusAsync, SetNxmHandlerAsync,
                () => OpenGameFolder(), () => OpenInbox(), LoadStorageAsync, PurgeAsync, CopyDiagnostics, RemoveFromPcAsync))
        { CloseCommand = new RelayCommand(() => Overlay = null) };
        Overlay = vm;
        vm.LoadStorageLater();
        if (vm.HasNexusKey) _ = CheckNexusAsync(vm);
    }

    private static bool SafeNxmRegistered() { try { return NxmProtocol.IsRegistered; } catch (Exception) { return false; } }
    private static string? SafeOtherHandler() { try { return NxmProtocol.OtherHandler(); } catch (Exception) { return null; } }

    private async Task ChangeGameFolderAsync(SettingsVm vm, string folder)
    {
        try
        {
            await _host.RunAsync(c => c.ChangeGameRoot(folder));
            vm.GameFolder = folder;
            vm.Message = "Game folder changed. The new folder was scanned; nothing in it was changed.";
            vm.MessageTone = Tone.Ok;
            await RefreshAsync();
            ScanCompleted?.Invoke();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            vm.Message = ex.Message;
            vm.MessageTone = Tone.Block;
        }
    }

    // ---------------- Nexus Mods (P24) ----------------
    private static string AppVersion => AppInfo.Version;

    private async Task SaveNexusKeyAsync(SettingsVm vm, string key)
    {
        vm.NexusStatus = "Checking the key with Nexus Mods…";
        vm.NexusTone = Tone.Info;
        try
        {
            using var client = new NexusClient(key, AppVersion);
            var acc = await client.ValidateAsync();
            NexusKeyStore.Save(key);
            vm.HasNexusKey = true;
            SetAccount(acc, vm);
        }
        catch (NexusException ex)
        {
            vm.NexusStatus = "Not saved: " + ex.Message;
            vm.NexusTone = Tone.Block;
        }
    }

    private void ForgetNexusKey()
    {
        NexusKeyStore.Clear();
        _nexusAccount = null;
        NexusBadge = "NEXUS · no key";
        NexusTone = Tone.Neutral;
    }

    /// <summary>Asks Nexus who owns the key: name and Premium. Runs at start and when Settings opens.</summary>
    private async Task CheckNexusAsync(SettingsVm? vm)
    {
        var key = NexusKeyStore.Load();
        if (key is null)
        {
            ForgetNexusKey();
            if (vm is not null) { vm.HasNexusKey = false; vm.NexusStatus = "No key yet"; vm.NexusTone = Tone.Neutral; }
            return;
        }
        try
        {
            using var client = new NexusClient(key, AppVersion);
            SetAccount(await client.ValidateAsync(), vm);
        }
        catch (NexusException ex)
        {
            NexusBadge = "NEXUS · offline";
            NexusTone = Tone.Warn;
            if (vm is not null) { vm.NexusStatus = ex.Message; vm.NexusTone = Tone.Warn; }
        }
    }

    private void SetAccount(NexusAccount acc, SettingsVm? vm)
    {
        _nexusAccount = acc;
        OnPropertyChanged(nameof(CanUpdateAll));
        NexusBadge = $"NEXUS · {acc.Tier.ToUpperInvariant()}";
        NexusTone = acc.IsPremium ? Tone.Ok : Tone.Info;
        if (vm is null) return;
        vm.NexusTier = acc.Tier;
        vm.NexusTone = acc.IsPremium ? Tone.Ok : Tone.Info;
        vm.NexusStatus = acc.IsPremium
            ? $"{acc.Name} · Premium. Mod Manager Download links install straight away; direct downloads from AFTERLIFE work too."
            : $"{acc.Name} · {acc.Tier}. Nexus allows free accounts to download only from the website: click Mod Manager Download and the mod arrives here.";
    }

    private Task SetNxmHandlerAsync(SettingsVm vm, bool on)
    {
        try
        {
            if (on) NxmProtocol.Register(); else NxmProtocol.Unregister();
            vm.Message = on ? "Mod Manager Download links now open AFTERLIFE." : "AFTERLIFE no longer handles Mod Manager Download links.";
            vm.MessageTone = Tone.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            vm.SetNxmSilently(!on);
            vm.Message = "Windows refused the change: " + ex.Message;
            vm.MessageTone = Tone.Block;
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void GetNewMods() => AppLinks.Open(NexusClient.CyberpunkPage);

    [RelayCommand]
    private void OpenNexusPage(string? url) { if (!string.IsNullOrWhiteSpace(url)) AppLinks.Open(url); }

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

    /// <summary>Downloads one Nexus file into library/downloads and records it in the history (P48). Null when it failed (message shown).</summary>
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

    /// <summary>Imports a downloaded Nexus file; a newer file of a mod already in the library replaces it in the profile (M4).</summary>
    private async Task<List<PackageManifest>> ImportDownloadedAsync(NxmLink link, string dest, NexusFileInfo info,
        IReadOnlyList<FomodChoice>? presetChoices = null, bool enable = true, bool quiet = false)
    {
        // P51: a file a collection is waiting for goes to that collection
        if (_collectionWaiting is { } waiting && waiting(link, dest, info)) return [];
        var older = OlderVersionOf(link.ModId, link.FileId);
        var imported = await ImportFilesCoreAsync([dest], new NexusSource(link.ModId, link.FileId, info.ModName ?? older?.ModName, info.Version ?? info.ModVersion, older?.ModId),
            presetChoices ?? older?.FomodChoices, enable, quiet);
        if (older is not null && imported.Count == 1 && imported[0].PackageId != older.PackageId) await ReplaceOlderAsync(older, imported[0], quiet);
        return imported;
    }

    /// <summary>The newest package of this Nexus mod in the library (another file than the one coming in).</summary>
    private PackageManifest? OlderVersionOf(string nexusModId, string? fileId) =>
        _overview?.Mods.Select(m => m.Package)
            .Where(p => p.LayerOfModId is null && NexusInfo.IdOf(p).Id == nexusModId && (fileId is null || p.NexusFileId != fileId))
            .OrderByDescending(p => p.ImportedAt).FirstOrDefault();

    /// <summary>M4/P63: the new version takes the place of the old one in the profile; the old one stays in the library, switched off.</summary>
    private async Task ReplaceOlderAsync(PackageManifest older, PackageManifest newer, bool quiet)
    {
        if (_overview is null) return;
        var wasOn = !IsModsOff && _overview.Status.Selected.IsEnabled(older.PackageId);
        // P98: the old version is in a collection: ask what the new one does there
        var collections = CollectionsOf(older.PackageId);
        var choice = collections.Count == 0 ? 0 : await AskCollectionUpdateAsync(older, newer, collections, batch: quiet);
        if (choice is null)
        {
            if (!quiet) Say($"{newer.ModName} {newer.Version} is in the library as a separate mod; the collection keeps {older.Version}.", Tone.Info);
            return;
        }
        await _host.RunAsync(c =>
        {
            c.SetEnabled(older.PackageId, false);
            if (wasOn) c.SetEnabled(newer.PackageId, true);
            NexusUpdates.Save(c.Store, older.PackageId, null, DateTimeOffset.UtcNow);
            if (collections.Count > 0 && choice is 0 or 1) c.ReplaceInCollections(older.PackageId, newer.PackageId);
        });
        // P95: the new version keeps the category of the old one
        if (_layout.CategoryIdOf(older.PackageId) is { } cat) await SaveLayoutAsync(_layout.Assign([newer.PackageId], cat));
        var deleted = false;
        if (choice == 1)
        {
            var r = await _host.RunAsync(c => c.DeleteEverywhere([older.PackageId]));
            deleted = r.Deleted.Count > 0;
        }
        await RefreshAsync();
        if (quiet) return;
        var where = collections.Count == 0 ? "" : choice == 2 ? " (outside the collection)" : $" in {string.Join(", ", collections)}";
        var old = deleted ? "The old version was deleted from the library." : "The old version stays in the library, switched off.";
        Say($"{older.ModName} {older.Version} → {newer.Version}{(wasOn ? " in the profile" : "")}{where}. {old} Apply to put it in the game.", Tone.Ok);
    }

    /// <summary>
    /// P63: a dropped archive named like a Nexus download of a mod already in the library is an update of that mod
    /// (old "Name-4885-0-2-27-1761517000" and new "Name-4885-0.2.27-2026-10-26T22-14Z-xxxx" file names).
    /// </summary>
    private async Task<List<string>> ImportUpdatesFromFilesAsync(IReadOnlyList<string> files)
    {
        var plain = new List<string>();
        foreach (var file in files)
        {
            var guess = PackageLibrary.GuessFromFileName(file);
            var older = guess.NexusModId is null ? null : OlderVersionOf(guess.NexusModId, null);
            if (older is null) { plain.Add(file); continue; }
            var version = guess.Version == "unknown" ? null : guess.Version;
            // the same Nexus page can also have optional files: ask, do not guess
            var replace = await AskAsync(new ConfirmVm("NEWER VERSION OF A MOD YOU HAVE?",
                $"{Path.GetFileName(file)} is a download of {older.ModName} (Nexus mod {guess.NexusModId}). Put it in the profile instead of {older.ModName} {older.Version}? The old version stays in the library, switched off. Cancel adds it as a separate mod.",
                "REPLACE OLD VERSION", false, [$"{older.ModName} {older.Version}   → {version ?? "new file"}"]));
            if (!replace) { plain.Add(file); continue; }
            var imported = await ImportFilesCoreAsync([file], new NexusSource(guess.NexusModId!, null, older.ModName, version, older.ModId), older.FomodChoices);
            if (imported.Count == 1 && imported[0].PackageId != older.PackageId) await ReplaceOlderAsync(older, imported[0], quiet: false);
        }
        return plain;
    }

    /// <summary>P51: set while a collection downloads; returns true when it took the file.</summary>
    private Func<NxmLink, string, NexusFileInfo, bool>? _collectionWaiting;

    private void SetReduceMotion(bool reduce)
    {
        Controls.Fx.SetReduceMotion(reduce);
        var current = AppSettings.Load();
        (current with { LibraryRoot = current.LibraryRoot ?? AppSettings.ResolveLibrary(null), ReduceMotion = reduce }).Save();
    }

    [RelayCommand]
    private Task NewProfile() => RunUiAsync(async () =>
    {
        var name = await AskAsync(new TextPromptVm("NEW PROFILE", "Profile name", $"Profile {Profiles.Count(p => !p.IsSystem) + 1}"));
        if (name is null) return;
        var copyFrom = SelectedProfile is { IsSystem: false } s ? s.Id : null;
        var created = await _host.RunAsync(c => c.CreateProfile(name, copyFrom));
        await _host.RunAsync(c => c.SelectProfile(created.Id));
        await RefreshAsync();
        Say($"Profile {name} created{(copyFrom is null ? "" : " as a copy")}. The game changes only when you apply.", Tone.Info);
    });

    // ---------------- P86: mod picture ----------------
    private async Task LoadImageAsync(DetailsVm details, ModOverview o)
    {
        if (o.Nexus is not { PictureUrl: { } url } n || _host.Library is not { } lib) return;
        try
        {
            var path = ModImageCache.Cached(lib, n.ModId, url) ?? await ModImageCache.EnsureAsync(lib, n.ModId, url);
            if (path is null || Details != details) return;
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; // the file stays free
            bmp.DecodePixelWidth = 480;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            details.Image = bmp;
        }
        catch (Exception ex) when (ex is IOException or System.Net.Http.HttpRequestException or TaskCanceledException or UnauthorizedAccessException or NotSupportedException or UriFormatException or InvalidOperationException) { }
    }

    // ---------------- P76: rename a mod ----------------
    public Task RenameModAsync(string packageId) => RunUiAsync(async () =>
    {
        var m = _overview?.Mods.FirstOrDefault(x => x.Package.PackageId == packageId);
        if (m is null) return;
        var name = await AskAsync(new TextPromptVm("RENAME MOD", "Name", m.Package.ModName) { ConfirmLabel = "RENAME", MaxLength = 120, AllowEmpty = true });
        if (name is null || name == m.Package.ModName) return;
        await _host.RunAsync(c => c.RenamePackage(packageId, name));
        await RefreshAsync();
    });

    // ---------------- P72: delete a profile ----------------
    /// <summary>P75: offered only when it can be done: not Mods off, not the profile that is in the game.</summary>
    public bool CanDeleteProfile => SelectedProfile is { IsSystem: false } p && _overview is not null
        && !(_overview.Profiles.FirstOrDefault(x => x.Id == p.Id) is { IsDeployed: true } && _overview.Mods.Any(m => m.InGame));

    [RelayCommand]
    private Task DeleteProfile() => RunUiAsync(async () =>
    {
        if (SelectedProfile is not { IsSystem: false } p || _overview is null) return;
        var summary = _overview.Profiles.FirstOrDefault(x => x.Id == p.Id);
        if (summary is { IsDeployed: true } && _overview.Mods.Any(m => m.InGame))
        {
            Say($"{p.Name} is in the game now. Select another profile (or Mods off) and apply, then delete {p.Name}.", Tone.Warn);
            return;
        }
        var ok = await AskAsync(new ConfirmVm("DELETE PROFILE?",
            $"{p.Name} and its choices (which mods are on, winners of conflicts, load order) are deleted. The mods stay in the library and in other profiles. This cannot be undone.",
            "DELETE PROFILE", true));
        if (!ok) return;
        await _host.RunAsync(c => c.DeleteProfile(p.Id));
        await RefreshAsync();
        Say($"Profile {p.Name} deleted. The mods stay in the library.", Tone.Ok);
    });

    // ---------------- import ----------------
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
            var allowUnverified = false;
            IReadOnlyList<FomodFile>? fomodFiles = null;
            IReadOnlyList<FomodChoice>? fomodChoices = null;
            while (true)
            {
                Say($"Importing {Path.GetFileName(file)}…", Tone.Info);
                var r = await _host.RunAsync(c => c.Import(new ImportRequest(file, ModName: nexus?.ModName, Version: nexus?.Version, ExistingModId: nexus?.ExistingModId,
                    Options: new ImportOptions(root, allowUnverified) { FomodFiles = fomodFiles }, NexusModId: nexus?.ModId, NexusFileId: nexus?.FileId)
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
                    // R3: the mod's own installer
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
                if (false)
                {
                    allowUnverified = await AskAsync(new ConfirmVm("UNTESTED FRAMEWORK",
                        r.Analysis.Problems.FirstOrDefault() + "\n\nYou can import it for testing on a copy of the game. Turning it off may leave files behind that AFTERLIFE does not know about yet.",
                        "IMPORT FOR TESTING"));
                    if (!allowUnverified) { failed.Add($"{Path.GetFileName(file)}: cancelled"); break; }
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
        // P18: one line per mod, or nothing when there is nothing to say
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

    // ---------------- apply / recovery / launch ----------------
    /// <summary>P20a: asks before writing to the game (deck).</summary>
    [RelayCommand]
    private Task ApplyChanges() => RunUiAsync(async () =>
    {
        if (_overview is null) return;
        var name = SelectedProfile?.Name ?? "";
        var added = _overview.Mods.Count(m => m.Wanted && !m.InGame);
        var removed = _overview.Mods.Count(m => !m.Wanted && m.InGame);
        var lines = new List<string>();
        if (added > 0) lines.Add($"+ {added} mod(s) into the game");
        if (removed > 0) lines.Add($"− {removed} mod(s) out of the game");
        if (_overview.OrderIsCustom) lines.Add("load order from the list (modlist.txt)");
        lines.AddRange(TwinWarnings()); // P98: two versions of one mod would go into the game
        foreach (var n in LeavingCore().Select(c => c.Name)) lines.Add($"{n}: files it made itself (logs, settings) go to the library, restorable");
        var msg = IsModsOff
            ? "Every mod AFTERLIFE put in the game will be taken out. Manually installed mods stay."
            : $"Load {name} to the deck? Only files AFTERLIFE knows are written; every file is verified afterwards.";
        // P78: "Load to deck"
        if (!await AskAsync(new ConfirmVm(IsModsOff ? "SWITCH THE GAME TO MODS OFF?" : $"LOAD {name.ToUpperInvariant()} TO THE DECK?", msg,
                IsModsOff ? "MODS OFF" : "LOAD TO DECK", lines: lines))) return;
        await ApplyCoreAsync(PlanDecisions.None);
    });

    [RelayCommand]
    private Task Preview() => RunUiAsync(async () =>
    {
        var r = await _host.RunAsync(c => c.Preview());
        Overlay = new PlanPreviewVm(SelectedProfile?.Name ?? "", r) { CloseCommand = new RelayCommand(() => Overlay = null) };
    });

    /// <summary>Core mods that the next apply takes out of the game (P37).</summary>
    private List<(string Capability, string Name)> LeavingCore()
    {
        if (_overview is null) return [];
        return _overview.Mods.Where(m => m.IsCore && m.InGame && (!m.Wanted || IsModsOff))
            .SelectMany(m => m.Package.Provides.Where(c => c.StartsWith("framework:", StringComparison.Ordinal)))
            .Distinct().Select(c => (c, Capabilities.DisplayName(c))).ToList();
    }

    /// <summary>P37: after a core mod left the game, its own runtime files are moved aside (restorable).</summary>
    private async Task<List<string>> CleanLeftoversAsync(IEnumerable<string> capabilities)
    {
        var lines = new List<string>();
        foreach (var cap in capabilities.Distinct())
        {
            try
            {
                var r = await _host.RunAsync(c => c.CleanLeftovers(cap));
                if (r is { Moved: > 0 }) lines.Add($"{Capabilities.DisplayName(cap)}: moved {r.Moved} file(s) it made itself to the library (restore in Scan manual mods).");
            }
            catch (Exception ex) when (IsExpected(ex)) { lines.Add($"{Capabilities.DisplayName(cap)}: its own files stayed in the game ({ex.Message})."); }
        }
        return lines;
    }

    private async Task ApplyCoreAsync(PlanDecisions decisions)
    {
        var leaving = LeavingCore().Select(c => c.Capability).ToList();
        IsOperating = true;
        CanApply = false;
        CanLaunch = false;
        BeginBreach($"WRITING · {SelectedProfile?.Name}", _overview?.PendingChanges ?? 0);
        SetPhase(DeployPhase.Preparing, 0, 0);
        try
        {
            var progress = new Progress<DeployProgress>(p => SetPhase(p.Phase, p.Done, p.Total, p.Path, p.Kind));
            var r = await _host.RunAsync(c => c.Apply(null, decisions, progress));
            if (r.Succeeded)
            {
                SetPhase(DeployPhase.Done, 0, 0);
                EndBreach(true, r.Execution is null ? "nothing to write" : $"{r.Execution.StepsApplied} change(s) verified");
                var done = new List<string> { r.Message == "already deployed" ? "The game already matches this profile." : "Done. The game matches the profile and every file was verified." };
                done.AddRange(await CleanLeftoversAsync(leaving));
                SayLines(done, Tone.Ok);
                ScanCompleted?.Invoke();
            }
            else if (r.Execution is not null)
            {
                PhaseFailed();
                EndBreach(false, "recovery needed");
                Say("The change did not finish: " + r.Message, Tone.Block);
            }
            else
            {
                PhaseIdle();
                CancelBreach();
                var modified = r.Preview?.Plan.Blockers.Where(b => b.Kind == IssueKind.ModifiedManagedFile && b.Path is not null).ToList() ?? [];
                if (modified.Count > 0 && decisions.DiscardChangesPathKeys.Count == 0)
                {
                    string NameOf(string? id) => _overview?.Mods.FirstOrDefault(m => m.Package.PackageId == id)?.Package.ModName ?? "Other files";
                    var items = await AskAsync(new DecisionsVm(modified.Select(b => (b.Path!, NameOf(b.PackageId)))));
                    if (items is null) { Say("Nothing was changed.", Tone.Neutral); return; }
                    var keep = items.Where(i => i.Keep).Select(i => i.Path).ToList();
                    var discard = items.Where(i => !i.Keep).Select(i => GamePath.Parse(i.Path).Key).ToHashSet();
                    if (keep.Count > 0) await _host.RunAsync(c => c.CaptureChanges(keep));
                    await ApplyCoreAsync(new PlanDecisions(discard));
                    return;
                }
                Say("Nothing was changed: " + r.Message.Replace("blocked: ", ""), Tone.Block);
            }
        }
        catch (InstallBusyException ex)
        {
            PhaseIdle();
            CancelBreach();
            Say(ex.Message, Tone.Warn);
        }
        catch
        {
            PhaseFailed();
            EndBreach(false, "stopped");
            throw;
        }
        finally
        {
            IsOperating = false;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private Task RecoverFinish() => RecoverAsync(RecoveryChoice.Finish);

    [RelayCommand]
    private Task RecoverReturn() => RecoverAsync(RecoveryChoice.ReturnToPreviousGeneration);

    private Task RecoverAsync(RecoveryChoice choice) => RunUiAsync(async () =>
    {
        IsOperating = true;
        BeginBreach(choice == RecoveryChoice.Finish ? "RECOVERY · finishing" : "RECOVERY · returning", 0);
        try
        {
            var progress = new Progress<DeployProgress>(p => SetPhase(p.Phase, p.Done, p.Total, p.Path, p.Kind));
            var (outcome, follow) = await _host.RunAsync(c => c.Recover(choice, progress));
            EndBreach(follow.Succeeded && outcome.UnknownPaths.Count == 0, follow.Succeeded ? "state verified" : "needs attention");
            if (outcome.UnknownPaths.Count > 0)
                Say($"Recovered the record, but {outcome.UnknownPaths.Count} file(s) have unknown content and were left untouched: {string.Join(", ", outcome.UnknownPaths.Take(3))}. Check them in Inbox.", Tone.Warn);
            else if (follow.Succeeded) { SetPhase(DeployPhase.Done, 0, 0); Say("Recovered. The game is in a known, verified state.", Tone.Ok); }
            else { PhaseFailed(); Say("Recovery needs attention: " + follow.Message, Tone.Block); }
        }
        finally
        {
            IsOperating = false;
            await RefreshAsync();
        }
    });

    [RelayCommand]
    private Task Launch() => LaunchCoreAsync(false);

    [RelayCommand]
    private Task LaunchAnyway() => RunUiAsync(async () =>
    {
        var ok = await AskAsync(new ConfirmVm("LAUNCH ANYWAY?",
            "The game was updated since you last reviewed it. Mods may crash or behave oddly until they are updated. Nothing will be switched off.",
            "LAUNCH ANYWAY"));
        if (ok) await LaunchCoreAsync(true);
    });

    private Task LaunchCoreAsync(bool allowChangedBuild) => RunUiAsync(async () =>
    {
        // R2: REDmods are compiled by redMod.exe and need -modded
        var mods = await _host.RunAsync(c => c.DeployedRedMods());
        var args = "--launcher-skip";
        string? note = null;
        if (mods.Count > 0)
        {
            Say($"Deploying {mods.Count} REDmod(s)… this can take a few minutes the first time.", Tone.Info);
            IsOperating = true;
            RedModResult r;
            try { r = await _host.RunAsync(c => c.DeployRedMods()); }
            finally { IsOperating = false; }
            if (r.LaunchModded) args += " -modded";
            else if (r.Outcome == RedModOutcome.Failed)
            {
                var go = await AskAsync(new ConfirmVm("REDMOD DEPLOY FAILED", r.Message + " Start the game without the REDmods?", "START WITHOUT REDMODS", false,
                    mods.Select(m => "mods/" + m).ToList(), null, true, "REDMODS"));
                if (!go) { Say(r.Message, Tone.Block); return; }
            }
            note = r.Outcome == RedModOutcome.ToolMissing ? r.Message : r.Outcome == RedModOutcome.Deployed ? r.Message + "." : null;
        }
        await _host.RunAsync(c => c.LaunchGame(args, allowChangedBuild));
        if (note is null) Say("Wake up, choom. The game is starting.", Tone.Ok);
        else SayLines(["Wake up, choom. The game is starting.", note], Tone.Ok);
    });

    [RelayCommand]
    private Task AcknowledgeBuild() => RunUiAsync(async () =>
    {
        var ok = await AskAsync(new ConfirmVm("MARK BUILD AS REVIEWED?",
            "Confirm that you checked your mods for the new game version (for example on their Nexus pages). This only removes the warning; nothing else changes.",
            "MARK AS REVIEWED"));
        if (!ok) return;
        await _host.RunAsync(c => c.AcknowledgeBuild());
        await RefreshAsync();
    });

    [RelayCommand]
    private Task SwitchToModsOff() => RunUiAsync(async () =>
    {
        await _host.RunAsync(c => c.SelectProfile(ProfileState.ModsOffId));
        await RefreshAsync();
        await ApplyCoreAsync(PlanDecisions.None);
    });

    // ---------------- panels ----------------
    [RelayCommand]
    private void OpenInbox()
    {
        if (_overview is null) return;
        var vm = new InboxVm(this) { CloseCommand = new RelayCommand(() => Overlay = null) };
        vm.Update(_overview);
        Overlay = vm;
    }

    [RelayCommand]
    private void OpenAbout() => Overlay = new AboutVm { CloseCommand = new RelayCommand(() => Overlay = null) };

    /// <summary>P96: the legend left the bottom bar; it lives in a guide behind the GUIDE tab.</summary>
    [RelayCommand]
    private void OpenGuide() => Overlay = new GuideVm { CloseCommand = new RelayCommand(() => Overlay = null) };

    [RelayCommand] private void OpenSupport() => AppLinks.Open(AppLinks.Sponsors);
    [RelayCommand] private void OpenContact() => AppLinks.Open(AppLinks.ContactMailto);
    private void OpenGameFolder() { if (Directory.Exists(GameFolder)) AppLinks.OpenFolder(GameFolder); }
    [RelayCommand] private Task Refresh() => RunUiAsync(RefreshAsync);
    [RelayCommand] private void DismissNotice() { Notice = null; NoticeLines.Clear(); }

    // ---------------- helpers ----------------
    private async Task<T> AskAsync<T>(OverlayVm vm, Func<OverlayVm, Task<T>> result)
    {
        var previous = Overlay;
        Overlay = vm;
        try { return await result(vm); }
        finally { Overlay = previous; }
    }

    private Task<bool> AskAsync(ConfirmVm vm) => AskAsync(vm, v => ((ConfirmVm)v).Result);
    private Task<string?> AskAsync(TextPromptVm vm) => AskAsync(vm, v => ((TextPromptVm)v).Result);
    private Task<string?> AskAsync(VariantChoiceVm vm) => AskAsync(vm, v => ((VariantChoiceVm)v).Result);
    private Task<IReadOnlyList<FomodChoice>?> AskAsync(FomodVm vm) => AskAsync(vm, v => ((FomodVm)v).Result);
    private Task<IReadOnlyList<DecisionItemVm>?> AskAsync(DecisionsVm vm) => AskAsync(vm, v => ((DecisionsVm)v).Result);

    private void Say(string text, Tone tone) => SayLines([text], tone);

    /// <summary>P18: each item on its own line; long reports are cut to 12 lines.</summary>
    private void SayLines(IReadOnlyList<string> lines, Tone tone)
    {
        NoticeLines.Clear();
        var shown = lines.Count > 12 ? lines.Take(11).Append($"  … and {lines.Count - 11} more").ToList() : lines.ToList();
        foreach (var l in shown) NoticeLines.Add(l);
        Notice = string.Join("\n", shown);
        NoticeTone = tone;
    }

    // ---------------- breach panel helpers ----------------
    private void BeginBreach(string title, int total)
    {
        _breachRun++;
        BreachLines.Clear();
        BreachTitle = title;
        BreachTone = Tone.Info;
        BreachDone = 0;
        BreachTotal = Math.Max(total, 1);
        BreachActive = true;
        ShowBreach = true;
        AddBreachLine("> JACKING INTO GAME FOLDER", Tone.Neutral);
    }

    private void AddBreachLine(string text, Tone tone)
    {
        BreachLines.Add(new LineVm(text, "", tone));
        while (BreachLines.Count > 9) BreachLines.RemoveAt(0);
    }

    private async void EndBreach(bool ok, string detail)
    {
        var run = _breachRun;
        BreachActive = false;
        BreachDone = BreachTotal;
        BreachTone = ok ? Tone.Ok : Tone.Block;
        BreachTitle = ok ? $"ACCESS GRANTED · {detail}" : $"BREACH INTERRUPTED · {detail}";
        AddBreachLine(ok ? "✓ VERIFIED" : "× STOPPED · nothing unknown was overwritten", ok ? Tone.Ok : Tone.Block);
        await Task.Delay(ok ? 1600 : 4500);
        if (run == _breachRun) ShowBreach = false;
    }

    private void CancelBreach()
    {
        _breachRun++;
        BreachActive = false;
        ShowBreach = false;
    }

    private static string Verb(StepKind? k) => k switch
    {
        StepKind.Add => "WRITE  ",
        StepKind.Replace => "INJECT ",
        StepKind.Remove => "PURGE  ",
        _ => "SYNC   ",
    };

    private void SetPhase(DeployPhase phase, int done, int total, string? path = null, StepKind? kind = null)
    {
        if (ShowBreach)
        {
            if (total > 0) { BreachTotal = total; BreachDone = done; }
            if (path is not null) AddBreachLine($"> {Verb(kind)} {path}", kind == StepKind.Remove ? Tone.Warn : Tone.Info);
            else if (phase == DeployPhase.Verifying) AddBreachLine("> VERIFY  hashes of every written file", Tone.Neutral);
        }
        var profile = SelectedProfile?.Name ?? "";
        switch (phase)
        {
            case DeployPhase.Preparing:
                PhaseTitle = $"PREPARING · {profile}"; PhaseTone = Tone.Info;
                Seg1 = Tone.Info; Seg2 = Tone.Neutral; Seg3 = Tone.Neutral; Seg2Label = "Applying";
                break;
            case DeployPhase.Applying:
                PhaseTitle = $"APPLYING · {profile}"; PhaseTone = Tone.Info;
                Seg1 = Tone.Ok; Seg2 = Tone.Info; Seg3 = Tone.Neutral; Seg2Label = total > 0 ? $"Applying {done}/{total}" : "Applying";
                break;
            case DeployPhase.Verifying:
                PhaseTitle = $"VERIFYING · {profile}"; PhaseTone = Tone.Info;
                Seg1 = Tone.Ok; Seg2 = Tone.Ok; Seg3 = Tone.Info;
                break;
            case DeployPhase.Done:
                PhaseTitle = $"VERIFIED · {profile}"; PhaseTone = Tone.Ok;
                Seg1 = Tone.Ok; Seg2 = Tone.Ok; Seg3 = Tone.Ok; Seg2Label = "Applying";
                break;
        }
    }

    private void PhaseIdle()
    {
        PhaseTitle = "READY"; PhaseTone = Tone.Neutral;
        Seg1 = Seg2 = Seg3 = Tone.Neutral; Seg2Label = "Applying";
    }

    private void PhaseFailed()
    {
        PhaseTitle = "STOPPED · recovery needed"; PhaseTone = Tone.Block;
        if (Seg2 == Tone.Info) Seg2 = Tone.Block; else if (Seg3 == Tone.Info) Seg3 = Tone.Block; else Seg1 = Tone.Block;
    }

    private async Task RunUiAsync(Func<Task> work)
    {
        IsBusy = true;
        try { await work(); }
        catch (Exception ex) when (IsExpected(ex)) { Say(ex.Message, Tone.Block); }
        finally { IsBusy = false; }
    }

    private static bool IsExpected(Exception ex) =>
        ex is InvalidOperationException or IOException or UnauthorizedAccessException or ImportException or InstallBusyException
            or DirectoryNotFoundException or Microsoft.Data.Sqlite.SqliteException;

    public void Dispose() => _host.Dispose();
}
