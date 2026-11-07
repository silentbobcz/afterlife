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

    public ObservableList<ProfileVm> Profiles { get; } = new();

    public ObservableList<ModRowVm> Mods { get; } = new();

    public ICollectionView ModsView { get; }

    public ObservableList<FrameworkStatus> Frameworks { get; } = new();

    /// <summary>Core table in the header (installed and missing base mods).</summary>
    public ObservableList<CoreModVm> CoreMods { get; } = new();

    /// <summary>Folders the scan looks at, with the number of files AFTERLIFE did not install.</summary>
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

    /// <summary>Notices are a list, one thing per line.</summary>
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

    // breach panel
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

    /// <summary>The mod behind the first blocker, so one click shows it in the list.</summary>
    [ObservableProperty] private string? _blockedPackageId;

    public bool CanShowBlocked => BlockedPackageId is not null;

    partial void OnBlockedPackageIdChanged(string? value) => OnPropertyChanged(nameof(CanShowBlocked));

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

    public async Task StartAsync()
    {
        Controls.Fx.SetReduceMotion(AppSettings.Load().ReduceMotion);
        ActivityLog.Directory = App.LogDirectory;
        ActivityLog.Enabled = AppSettings.Load().ActivityLog;
        ActivityLog.Write("app", $"start · AFTERLIFE {AppVersion}");
        var lib = AppSettings.ResolveLibrary(null);
        if (!AppSettings.IsInitialized(lib))
        {
            Overlay = new FirstRunVm(InitializeAsync);
            return;
        }
        await RunUiAsync(async () =>
        {
            await _host.OpenAsync(lib);
            await RefreshAsync(rescan: true);
            ScanCompleted?.Invoke();
        });
        _ = AfterOpenAsync();
        if (_pendingLink is not null) { var l = _pendingLink; _pendingLink = null; await HandleNxmAsync(l); }
    }

    /// <summary>After opening: new requirement rules for old mods, Nexus account and mod pages.</summary>
    private async Task AfterOpenAsync()
    {
        try
        {
            if (await _host.RunAsync(c => c.RefreshRequirements()) > 0) await RefreshAsync();
        }
        catch (Exception ex) when (IsExpected(ex)) { }
        await CheckDuplicatesAsync(); 
        await CheckOldVersionsAsync(); 
        await CheckNexusAsync(null);
        await FetchNexusInfoAsync(onlyMissing: true);
        await CheckUpdatesAsync(force: false);
        StartUpdateTimer();
    }

    [ObservableProperty] private int _updateCount;

    [RelayCommand]
    private Task RefreshLibrary() => RunUiAsync(async () =>
    {
        Say("Refreshing: reading the game folder and the library again…", Tone.Info);
        try { await _host.RunAsync(c => c.RefreshRequirements()); }
        catch (Exception ex) when (IsExpected(ex)) { }
        await RefreshAsync(rescan: true);
        ScanCompleted?.Invoke();
        await FetchNexusInfoAsync(onlyMissing: true);
        await CheckUpdatesAsync(force: false);
        Say("Refreshed. The list shows the game folder and the library as they are now.", Tone.Ok);
    });

    /// <summary>Links a manual import to its Nexus page by id or link.</summary>
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

    [ObservableProperty] private string? _toast;

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

    /// <summary>The detail tab the user picked; kept while switching between mods.</summary>
    public DetailTab? PreferredTab { get; set; }

    /// <summary>Nxm:// link from the command line or from a second AFTERLIFE start.</summary>
    private DateTimeOffset _lastActivationScan = DateTimeOffset.MinValue;

    /// <summary>
    /// The window came back to the front: the user may have changed files by hand or the game has just closed.
    /// A full scan, at most once in 15 seconds and never over a running operation or dialog.
    /// </summary>
    public async Task OnActivatedAsync()
    {
        if (!_host.IsOpen || IsBusy || IsOperating || Overlay is not null) return;
        var now = DateTimeOffset.UtcNow;
        if (now - _lastActivationScan < TimeSpan.FromSeconds(15)) return;
        _lastActivationScan = now;
        await RunUiAsync(() => RefreshAsync(rescan: true));
    }

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
            await RefreshAsync(rescan: true);
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

    /// <summary>
    /// Rebuilds the window from the library. By default the last scan of the game folder is reused when only
    /// Profiles changed (the coordinator walks the folder again as soon as packages or the ledger changed);
    /// <paramref name="rescan"/> forces the walk, for the moments when files may have changed outside AFTERLIFE:
    /// Start, REFRESH, Scan manual mods, the window coming back to the front.
    /// </summary>
    public async Task RefreshAsync(bool rescan = false)
    {
        if (!_host.IsOpen) return;
        var o = await _host.RunAsync(c => OverviewBuilder.Build(c, rescan));
        await LoadLayoutAsync(); // collections and the block layout decide the groups
        Apply(o);
        BuildCollectionChapters();
        PruneLayout();
        await SyncSortedOrderAsync(o);
    }

    private bool _syncingOrder;

    /// <summary>
    /// With a custom load order (modlist.txt) the list is the load order. MODS and categories are now sorted by
    /// Name, so the profile follows the list once: same blocks, alphabetical inside them. Apply puts it in the game.
    /// </summary>
    private async Task SyncSortedOrderAsync(InstallOverview o)
    {
        if (_syncingOrder || !o.OrderIsCustom || IsModsOff || o.Status.Unfinished is not null) return;
        var slugOfName = SlugOfName();
        var byId = Mods.ToDictionary(m => m.PackageId, m => m.Overview);
        var composed = BlockLayout.Compose(CurrentOrder(), id => byId.TryGetValue(id, out var m) ? SectionOf(m, _layout, slugOfName) : BlockLayout.ModsBlock,
            _layout.Sections(CollectionSlugs));
        var set = composed.ToHashSet(StringComparer.Ordinal);
        var now = o.Status.Selected.Entries.Select(e => e.PackageId).Where(set.Contains).ToList();
        if (now.SequenceEqual(composed)) return;
        _syncingOrder = true;
        try
        {
            await _host.RunAsync(c => c.Reorder(o.Status.Selected.Id, composed));
            await RefreshAsync();
        }
        catch (Exception ex) when (IsExpected(ex)) { }
        finally { _syncingOrder = false; }
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

            // Mods off first, then the own profiles alphabetically
            Profiles.Reset(o.Profiles.OrderBy(p => p.IsSystem ? 0 : 1).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Select(p => new ProfileVm(p,
                p.IsSystem ? "no managed mods in game" : $"{p.Wanted} mod{(p.Wanted == 1 ? "" : "s")} wanted",
                // a deployed profile with changes not applied yet is not "deployed" in the green sense
                p.IsDeployed && p.Id == st.Selected.Id && o.PendingChanges > 0 ? "PENDING" : p.IsDeployed ? "DEPLOYED" : (p.IsSystem ? "SYSTEM" : null),
                p.IsDeployed && p.Id == st.Selected.Id && o.PendingChanges > 0 ? Tone.Info : p.IsDeployed ? Tone.Ok : Tone.Neutral)));
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == st.Selected.Id);

            var keep = SelectedMod?.PackageId;
            var keepMulti = Mods.Where(m => m.IsChecked).Select(m => m.PackageId).ToHashSet();
            // rows survive a refresh: the same object is updated in place, so selection, checkbox and scroll position stay
            var existing = Mods.ToDictionary(m => m.PackageId, StringComparer.Ordinal);
            var editable = !st.Selected.IsSystemModsOff && st.Unfinished is null;
            var rows = o.Mods.Select(m =>
            {
                if (existing.TryGetValue(m.Package.PackageId, out var row)) { row.Update(m, editable); return row; }
                row = new ModRowVm(m, editable, OnWantChangedAsync);
                row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ModRowVm.IsChecked)) UpdateChecked(); };
                return row;
            }).ToList();
            AssignChapters(rows);
            MarkTwins(rows, o);
            // MODS and every category alphabetically by name (a layer "└ name" stays right below its mod).
            // Collections keep their (curator's) order and CORE MODS their fixed one: the stable sort leaves them as they are.
            var parentName = rows.Where(r => r.Overview.Package.LayerOfModId is null)
                .GroupBy(r => r.Overview.Package.ModId).ToDictionary(g => g.Key, g => g.First().Overview.Package.ModName, StringComparer.Ordinal);
            string Group(ModRowVm r) => !r.InModsBlock ? "" : r.Overview.Package.LayerOfModId is { } of && parentName.TryGetValue(of, out var pn) ? pn : r.Overview.Package.ModName;
            var byName = StringComparer.Create(System.Globalization.CultureInfo.CurrentCulture, ignoreCase: true);
            var ordered = rows.OrderBy(r => r.BlockRank).ThenBy(Group, byName)
                .ThenBy(r => !r.InModsBlock || r.Overview.Package.LayerOfModId is null ? 0 : 1)
                .ThenBy(r => r.InModsBlock ? r.Overview.Package.ModName : "", byName)
                .ThenBy(r => r.InModsBlock ? r.Overview.Package.Version : "", StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ordered.SequenceEqual(Mods)) ModsView.Refresh(); // same rows in the same order: regroup only
            else Mods.Reset(ordered);
            UpdateGroupNames();
            foreach (var m in Mods) m.IsChecked = keepMulti.Contains(m.PackageId);
            IsEmptyLibrary = Mods.Count == 0;
            UpdateCount = o.Mods.Count(m => m.Update is not null);
            OnPropertyChanged(nameof(CanUpdateAll));
            OnPropertyChanged(nameof(UpdateAllLabel));
            SelectedMod = Mods.FirstOrDefault(m => m.PackageId == keep) ?? Mods.FirstOrDefault();
            var previous = Details;
            Details = SelectedMod is null ? null : new DetailsVm(SelectedMod.Overview, this);
            if (Details is not null)
            {
                // same mod, same picture: keep the decoded bitmap instead of reading the file again
                if (previous?.Image is { } img && previous.Overview.Nexus?.PictureUrl is { } url && url == SelectedMod!.Overview.Nexus?.PictureUrl) Details.Image = img;
                else _ = LoadImageAsync(Details, SelectedMod!.Overview);
            }
            UpdateChecked();

            var wanted = o.Mods.Count(m => m.Wanted);
            var deployed = o.Mods.Count(m => m.InGame);
            LibrarySubtitle = $"{o.Mods.Count} mod{(o.Mods.Count == 1 ? "" : "s")} · {wanted} in profile {st.Selected.Name} · {deployed} in game"
                + (o.OrderIsCustom ? " · custom load order" : "");
            Frameworks.Reset(o.Frameworks);
            CoreMods.Reset(o.CoreMods.Select(c =>
            {
                // update of a core mod shows quietly in the header table too
                var row = c.PackageId is null ? null : o.Mods.FirstOrDefault(m => m.Package.PackageId == c.PackageId);
                return new CoreModVm(c, row?.Update, row?.Update is null ? null : new AsyncRelayCommand(() => UpdateModAsync(c.PackageId!)));
            }));
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
            // a blocker tied to a mod gets a SHOW button; environment issues (game running, ...) have none
            var blockedId = blocking.Select(b => b.PackageId).FirstOrDefault(id => id is not null && o.Mods.Any(m => m.Package.PackageId == id));
            BlockedPackageId = blockedId;
            CanLaunch = o.CanLaunch && !IsOperating;
            LaunchHint = o.LaunchHint;
        }
        finally { _building = false; }
        UpdateChecked(); // the count must follow the rebuilt list (deleted mods drop out)
        OnPropertyChanged(nameof(CanDeleteProfile));
    }

    [ObservableProperty] private bool _allChecked;

    public Task RescanAsync() => RunUiAsync(async () =>
    {
        await RefreshAsync(rescan: true);
        ScanCompleted?.Invoke();
    });

    private static string AppVersion => AppInfo.Version;

    [RelayCommand]
    private void GetNewMods() => AppLinks.Open(NexusClient.CyberpunkPage);

    [RelayCommand]
    private void OpenNexusPage(string? url) { if (!string.IsNullOrWhiteSpace(url)) AppLinks.Open(url); }

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

    [RelayCommand]
    private void ShowBlocked()
    {
        if (BlockedPackageId is null) return;
        var row = Mods.FirstOrDefault(m => m.PackageId == BlockedPackageId);
        if (row is null) return;
        ShowInChapter(row);
    }

    /// <summary>
    /// Rename and Delete are offered for every own profile (not Mods off). Deleting the profile that is
    /// In the game first switches the game to Mods off (its mods are taken out), then deletes it.
    /// </summary>
    public bool CanDeleteProfile => SelectedProfile is { IsSystem: false };

    [RelayCommand]
    private Task DeleteProfile() => RunUiAsync(async () =>
    {
        if (SelectedProfile is not { IsSystem: false } p || _overview is null) return;
        var summary = _overview.Profiles.FirstOrDefault(x => x.Id == p.Id);
        if (summary is { IsDeployed: true } && _overview.Mods.Any(m => m.InGame))
        {
            // in the game right now → offer the two steps as one action instead of a dead end.
            var go = await AskAsync(new ConfirmVm($"{p.Name.ToUpperInvariant()} IS IN THE GAME",
                $"To delete {p.Name}, AFTERLIFE first switches the game to Mods off (its mods are taken out of the game, every file verified), then deletes the profile. The mods stay in the library and in other profiles.",
                "MODS OFF, THEN DELETE", true) { CancelLabel = "Keep it" });
            if (!go) return;
            var modsOff = Profiles.FirstOrDefault(x => x.IsSystem);
            if (modsOff is null) { Say("Mods off profile not found.", Tone.Block); return; }
            await _host.RunAsync(c => c.SelectProfile(modsOff.Id));
            await RefreshAsync();
            await ApplyCoreAsync(PlanDecisions.None);
            var still = _overview?.Profiles.FirstOrDefault(x => x.Id == p.Id);
            if (still is { IsDeployed: true } && (_overview?.Mods.Any(m => m.InGame) ?? false))
            {
                Say($"{p.Name} is still in the game (the switch to Mods off did not finish), so it was not deleted.", Tone.Warn);
                return;
            }
            await _host.RunAsync(c => c.DeleteProfile(p.Id));
            await RefreshAsync();
            Say($"The game runs without mods now; profile {p.Name} deleted. The mods stay in the library.", Tone.Ok);
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

    /// <summary>The legend left the bottom bar; it lives in a guide behind the GUIDE tab.</summary>
    [RelayCommand]
    private void OpenGuide() => Overlay = new GuideVm { CloseCommand = new RelayCommand(() => Overlay = null) };

    [RelayCommand] private void OpenSupport() => AppLinks.Open(AppLinks.Sponsors);

    [RelayCommand] private void OpenContact() => AppLinks.Open(AppLinks.ContactMailto);

    private void OpenGameFolder() { if (Directory.Exists(GameFolder)) AppLinks.OpenFolder(GameFolder); }

    [RelayCommand] private Task Refresh() => RunUiAsync(() => RefreshAsync(rescan: true));

    private static bool IsExpected(Exception ex) =>
        ex is InvalidOperationException or IOException or UnauthorizedAccessException or ImportException or InstallBusyException
            or DirectoryNotFoundException or Microsoft.Data.Sqlite.SqliteException;

    public void Dispose() => _host.Dispose();
}
