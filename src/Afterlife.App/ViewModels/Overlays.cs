using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>
public abstract partial class OverlayVm : ObservableObject
{
    public abstract string Title { get; }
    public ICommand? CloseCommand { get; init; }
}

public sealed partial class FirstRunVm : OverlayVm
{
    private readonly Func<FirstRunVm, Task> _initialize;

    public FirstRunVm(Func<FirstRunVm, Task> initialize)
    {
        _initialize = initialize;
        _gameFolder = DetectGame() ?? "";
        SuggestLibrary();
        Validate();
    }

    public override string Title => "SET UP AFTERLIFE";

    [ObservableProperty] private string _gameFolder;
    [ObservableProperty] private string _libraryFolder = "";
    [ObservableProperty] private string? _gameStatus;
    [ObservableProperty] private Tone _gameTone;
    [ObservableProperty] private string? _warning;
    [ObservableProperty] private bool _canStart;
    [ObservableProperty] private bool _busy;

    [ObservableProperty] private string? _libraryNote;
    /// <summary>P33: optional personal Nexus Mods API key, asked together with the folders.</summary>
    [ObservableProperty] private string _nexusKey = "";
    public string NexusHint => "Optional. Your personal key from nexusmods.com (Settings → API Keys) lets AFTERLIFE download mods, read their descriptions and requirements. You can add it later in Settings.";
    [RelayCommand] private void OpenKeyPage() => AppLinks.Open(NexusClient.KeyPage);
    private bool _libraryEdited, _suggesting;

    partial void OnGameFolderChanged(string value)
    {
        if (!_libraryEdited) SuggestLibrary();
        Validate();
    }

    partial void OnLibraryFolderChanged(string value)
    {
        if (!_suggesting) _libraryEdited = true;
        Validate();
    }

    /// <summary>P2: library next to AFTERLIFE.exe when that place is usable, otherwise %LOCALAPPDATA%.</summary>
    private void SuggestLibrary()
    {
        _suggesting = true;
        var (path, reason) = AppSettings.SuggestLibrary(string.IsNullOrWhiteSpace(GameFolder) ? null : GameFolder);
        LibraryFolder = path;
        LibraryNote = reason is null
            ? "Next to AFTERLIFE.exe, so the app and its mods stay together (portable). Use a local NTFS drive, outside the game folder."
            : $"In your user profile, because {reason}. You can choose another folder.";
        _suggesting = false;
    }

    private void Validate()
    {
        Warning = null;
        if (string.IsNullOrWhiteSpace(GameFolder)) { GameStatus = "Choose the folder that contains bin\\x64\\Cyberpunk2077.exe"; GameTone = Tone.Neutral; CanStart = false; return; }
        var exe = Path.Combine(GameFolder, "bin", "x64", "Cyberpunk2077.exe");
        if (!File.Exists(exe)) { GameStatus = "Cyberpunk2077.exe not found in bin\\x64 of this folder"; GameTone = Tone.Block; CanStart = false; return; }
        var fp = new WindowsGameEnvironment().Fingerprint(GameFolder);
        GameStatus = $"Found Cyberpunk 2077 · {fp?.Display}";
        GameTone = Tone.Ok;
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if ((pf.Length > 0 && GameFolder.StartsWith(pf, StringComparison.OrdinalIgnoreCase)) || (pf86.Length > 0 && GameFolder.StartsWith(pf86, StringComparison.OrdinalIgnoreCase)))
            Warning = "The game is under Program Files. Windows may refuse writes without administrator rights; AFTERLIFE runs without them and will explain if a write is refused. Installing the game elsewhere avoids this.";
        var lib = Path.GetFullPath(string.IsNullOrWhiteSpace(LibraryFolder) ? AppSettings.DefaultLibraryRoot : LibraryFolder);
        var game = Path.GetFullPath(GameFolder);
        if (lib.StartsWith(game + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(lib, game, StringComparison.OrdinalIgnoreCase))
        { GameStatus = "The library must not be inside the game folder"; GameTone = Tone.Block; CanStart = false; return; }
        CanStart = true;
    }

    [RelayCommand]
    private async Task Start()
    {
        Busy = true;
        try { await _initialize(this); }
        finally { Busy = false; }
    }

    [RelayCommand]
    private void BrowseGame()
    {
        var d = new Microsoft.Win32.OpenFolderDialog { Title = "Choose the Cyberpunk 2077 folder" };
        if (Directory.Exists(GameFolder)) d.InitialDirectory = GameFolder;
        if (d.ShowDialog() == true) GameFolder = d.FolderName;
    }

    [RelayCommand]
    private void BrowseLibrary()
    {
        var d = new Microsoft.Win32.OpenFolderDialog { Title = "Choose where AFTERLIFE keeps imported mods" };
        if (d.ShowDialog() == true) LibraryFolder = Path.Combine(d.FolderName, "AFTERLIFE Library");
    }

    private static string? DetectGame()
    {
        // Simple check of the default store locations; full detection (Steam libraries, GOG, Epic) is milestone M5.
        var candidates = new List<string>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName))
        {
            candidates.Add(Path.Combine(drive, "Program Files (x86)", "Steam", "steamapps", "common", "Cyberpunk 2077"));
            candidates.Add(Path.Combine(drive, "SteamLibrary", "steamapps", "common", "Cyberpunk 2077"));
            candidates.Add(Path.Combine(drive, "Program Files (x86)", "GOG Galaxy", "Games", "Cyberpunk 2077"));
            candidates.Add(Path.Combine(drive, "GOG Games", "Cyberpunk 2077"));
            candidates.Add(Path.Combine(drive, "Program Files", "Epic Games", "Cyberpunk2077"));
            candidates.Add(Path.Combine(drive, "Games", "Cyberpunk 2077"));
        }
        return candidates.FirstOrDefault(c => File.Exists(Path.Combine(c, "bin", "x64", "Cyberpunk2077.exe")));
    }
}

public sealed partial class VariantChoiceVm : OverlayVm
{
    private readonly TaskCompletionSource<string?> _tcs = new();

    public VariantChoiceVm(string fileName, IReadOnlyList<string> variants, IReadOnlyList<string> problems)
    {
        FileName = fileName;
        Variants = variants;
        Problems = problems;
        _selected = variants.FirstOrDefault();
    }

    public override string Title => "CHOOSE ONE VARIANT";
    public string FileName { get; }
    public IReadOnlyList<string> Variants { get; }
    public IReadOnlyList<string> Problems { get; }
    public string Hint => "This archive contains several install options. Only one is installed; you can import the archive again for another.";
    public Task<string?> Result => _tcs.Task;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string? _selected;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => _tcs.TrySetResult(Selected);
    private bool CanConfirm() => Selected is not null;

    [RelayCommand]
    private void Cancel() => _tcs.TrySetResult(null);
}

public sealed partial class ConfirmVm : OverlayVm
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public ConfirmVm(string title, string message, string confirmLabel, bool danger = false,
        IReadOnlyList<string>? lines = null, string? optionLabel = null, bool optionChecked = true, string? linesTitle = null)
    {
        TitleText = title;
        Message = message;
        ConfirmLabel = confirmLabel;
        Danger = danger;
        Lines = lines ?? [];
        LinesTitle = linesTitle;
        OptionLabel = optionLabel;
        _optionChecked = optionChecked;
    }

    public string TitleText { get; }
    public override string Title => TitleText;
    public string Message { get; }
    public string ConfirmLabel { get; }
    public bool Danger { get; }
    /// <summary>One item per line (P18/P21): what exactly will happen.</summary>
    public IReadOnlyList<string> Lines { get; }
    public string? LinesTitle { get; }
    public bool HasLines => Lines.Count > 0;
    /// <summary>Optional checkbox, for example "also switch off the mods that need it" (P15).</summary>
    public string? OptionLabel { get; }
    [ObservableProperty] private bool _optionChecked;
    public Task<bool> Result => _tcs.Task;

    [RelayCommand] private void Confirm() => _tcs.TrySetResult(true);
    [RelayCommand] private void Cancel() => _tcs.TrySetResult(false);
}

public sealed partial class TextPromptVm : OverlayVm
{
    private readonly TaskCompletionSource<string?> _tcs = new();

    public TextPromptVm(string title, string label, string initial)
    {
        TitleText = title;
        Label = label;
        _text = initial;
    }

    public string TitleText { get; }
    public override string Title => TitleText;
    public string Label { get; }
    public Task<string?> Result => _tcs.Task;
    public string ConfirmLabel { get; init; } = "CREATE";
    public int MaxLength { get; init; } = 40;
    /// <summary>P76: an empty answer is allowed (rename: back to the original name).</summary>
    public bool AllowEmpty { get; init; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _text;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => _tcs.TrySetResult(Text.Trim());
    private bool CanConfirm() => (AllowEmpty || !string.IsNullOrWhiteSpace(Text)) && Text.Trim().Length <= MaxLength;

    [RelayCommand] private void Cancel() => _tcs.TrySetResult(null);
}

public sealed partial class DecisionItemVm : ObservableObject
{
    public DecisionItemVm(string path) => Path = path;
    public string Path { get; }
    [ObservableProperty] private bool _keep = true;
}

public sealed partial class DecisionGroupVm(string mod, IReadOnlyList<string> files) : ObservableObject
{
    public string Mod { get; } = mod;
    public IReadOnlyList<string> Files { get; } = files;
    public string Summary => Files.Count == 1 ? Files[0] : $"{Files.Count} files · {string.Join(", ", Files.Take(3).Select(f => f[(f.LastIndexOf('/') + 1)..]))}{(Files.Count > 3 ? ", …" : "")}";
    public string AllFiles => string.Join("\n", Files);
    [ObservableProperty] private bool _keep = true;
}

/// <summary>Files changed outside AFTERLIFE: keep the edits as a layer, or discard them.</summary>
public sealed partial class DecisionsVm : OverlayVm
{
    private readonly TaskCompletionSource<IReadOnlyList<DecisionItemVm>?> _tcs = new();

    /// <summary>P62: one choice per mod, not per file.</summary>
    public DecisionsVm(IEnumerable<(string Path, string Mod)> files) =>
        Groups = files.GroupBy(f => f.Mod).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DecisionGroupVm(g.Key, g.Select(f => f.Path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList())).ToList();

    public override string Title => "FILES CHANGED OUTSIDE AFTERLIFE";
    public string Hint => "Mods often change their own settings files. Keep: the changed files are saved in the library as \"my edits\" and stay with the mod. Discard: they are replaced or removed as planned. One choice per mod.";
    public IReadOnlyList<DecisionGroupVm> Groups { get; }
    public Task<IReadOnlyList<DecisionItemVm>?> Result => _tcs.Task;

    [RelayCommand] private void KeepAll() { foreach (var g in Groups) g.Keep = true; }
    [RelayCommand] private void DiscardAll() { foreach (var g in Groups) g.Keep = false; }
    [RelayCommand] private void Confirm() =>
        _tcs.TrySetResult(Groups.SelectMany(g => g.Files.Select(f => new DecisionItemVm(f) { Keep = g.Keep })).ToList());
    [RelayCommand] private void Cancel() => _tcs.TrySetResult(null);
}

public sealed partial class PriorityVm : OverlayVm
{
    private readonly Func<ArchiveOrderSettings, Task> _save;

    public PriorityVm(ArchiveOrderSettings current, IReadOnlyList<string> managed, IReadOnlyList<string> unmanaged,
        IReadOnlyList<string> conflictLines, bool editable, Func<ArchiveOrderSettings, Task> save)
    {
        _save = save;
        Editable = editable;
        _custom = current.Custom;
        _includeUnmanaged = current.IncludeUnmanagedArchives;
        _allowTakeover = current.AllowModlistTakeover;
        UnmanagedCount = unmanaged.Count;
        var all = managed.Concat(current.IncludeUnmanagedArchives ? unmanaged : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var first = current.Order.Where(o => all.Contains(o, StringComparer.OrdinalIgnoreCase)).ToList();
        var rest = all.Except(first, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        Archives = new ObservableCollection<string>(first.Concat(rest));
        ConflictLines = conflictLines;
    }

    public override string Title => "PRIORITY & CONFLICTS";
    public bool Editable { get; }
    public int UnmanagedCount { get; }
    public ObservableCollection<string> Archives { get; }
    public IReadOnlyList<string> ConflictLines { get; }
    public bool HasConflicts => ConflictLines.Count > 0;
    public string Honest => "Archive order matters only when two archives change the same game resource. AFTERLIFE does not look inside .archive files, so it cannot tell you when that happens.";

    [ObservableProperty] private bool _custom;
    [ObservableProperty] private bool _includeUnmanaged;
    [ObservableProperty] private bool _allowTakeover;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand), nameof(MoveDownCommand))]
    private string? _selected;

    [RelayCommand(CanExecute = nameof(CanUp))]
    private void MoveUp() => Move(-1);
    private bool CanUp() => Selected is not null && Archives.IndexOf(Selected) > 0;

    [RelayCommand(CanExecute = nameof(CanDown))]
    private void MoveDown() => Move(1);
    private bool CanDown() => Selected is not null && Archives.IndexOf(Selected) is var i && i >= 0 && i < Archives.Count - 1;

    private void Move(int d)
    {
        if (Selected is null) return;
        var s = Selected;
        var i = Archives.IndexOf(s);
        Archives.Move(i, i + d);
        Selected = s;
        Custom = true;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task Save() => _save(new ArchiveOrderSettings(Custom, Custom ? Archives.ToList() : [], IncludeUnmanaged, AllowTakeover));
}

public sealed partial class DetectedGroupVm : ObservableObject
{
    public DetectedGroupVm(DetectedMod m)
    {
        Mod = m;
        Files = m.Files.Select(f => new LineVm(f.Path, InboxVm.FormatSize(f.Size))).ToList();
    }

    public DetectedMod Mod { get; }
    public string Name => Mod.Name;
    public string KindLabel => Mod.Kind switch
    {
        DetectedKind.Archive => "Archive",
        DetectedKind.Redscript => "redscript",
        DetectedKind.Tweak => "Tweak",
        DetectedKind.Input => "Input",
        DetectedKind.CetMod => "CET mod",
        DetectedKind.Red4extPlugin => "RED4ext plugin",
        DetectedKind.Redmod => "REDmod",
        DetectedKind.Framework => "Core mod",
        _ => "File",
    };
    public string Summary => $"{Mod.Files.Count} file{(Mod.Files.Count == 1 ? "" : "s")} · {InboxVm.FormatSize(Mod.TotalSize)}";
    public bool Removable => Mod.Removable;
    public string? Note => Mod.Note;
    public IReadOnlyList<LineVm> Files { get; }

    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private bool _showFiles;
}

public sealed record RemovedBatchVm(RemovedBatch Batch, ICommand Restore)
{
    public string Label => Batch.Label;
    public string Detail => $"{Batch.Pending} file(s) · removed {Batch.RemovedAt.LocalDateTime:g}";
}

/// <summary>
/// P6/P16: "Scan manual mods" — mods found in the game that AFTERLIFE did not install, grouped; rescan with the
/// NETDECK network scan, reversible removal (core mods too, P15), restore.
/// </summary>
public sealed partial class InboxVm : OverlayVm
{
    private readonly MainViewModel _main;

    public InboxVm(MainViewModel main)
    {
        _main = main;
    }

    public override string Title => "SCAN MANUAL MODS";
    public string Hint => "Mods installed outside AFTERLIFE, grouped by name and folder. They stay untouched unless you remove them here. Removing moves the files into the library, so it can always be undone.";
    public ObservableCollection<DetectedGroupVm> Groups { get; } = new();
    public ObservableCollection<RemovedBatchVm> Removed { get; } = new();

    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasRemoved;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _scanLine = "> folder scan ready";
    [ObservableProperty] private IReadOnlyList<Controls.ScanFolder> _folders = MainViewModel.ScanFoldersOf([]);

    public void Update(InstallOverview o)
    {
        var checkedIds = Groups.Where(g => g.IsChecked).Select(g => g.Mod.Id).ToHashSet();
        Groups.Clear();
        foreach (var d in o.Detected.OrderBy(d => d.IsFramework ? 0 : 1).ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            var g = new DetectedGroupVm(d) { IsChecked = checkedIds.Contains(d.Id) && d.Removable };
            g.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DetectedGroupVm.IsChecked)) RemoveSelectedCommand.NotifyCanExecuteChanged(); };
            Groups.Add(g);
        }
        Removed.Clear();
        foreach (var b in o.RemovedBatches) Removed.Add(new RemovedBatchVm(b, new AsyncRelayCommand(() => _main.RestoreRemovedAsync(b))));
        var mods = o.Detected.Count(d => !d.IsFramework);
        var fw = o.Detected.Count(d => d.IsFramework);
        Summary = $"{mods} mod{(mods == 1 ? "" : "s")} · {fw} core mod{(fw == 1 ? "" : "s")} · {o.Unmanaged.Count} file{(o.Unmanaged.Count == 1 ? "" : "s")}";
        IsEmpty = o.Detected.Count == 0;
        HasRemoved = Removed.Count > 0;
        Folders = MainViewModel.ScanFoldersOf(o.Unmanaged.Select(u => u.Path));
        RemoveSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task Rescan()
    {
        if (IsScanning) return;
        IsScanning = true;
        try
        {
            // P74: reading the folders and rebuilding the list can hold the window for a moment, which froze the sweep
            // half way; so read first, then play one whole sweep over the fresh counts and end with the result
            ScanLine = "> reading the game folders …";
            await _main.RescanAsync();
            IsScanning = false;
            IsScanning = true; // restarts the sweep from the first folder
            foreach (var step in MainViewModel.ScanBuckets.Select(b => b.Label))
            {
                ScanLine = $"> probing {step} …";
                await Task.Delay(170);
            }
            await Task.Delay(250);
            ScanLine = $"> scan complete · {Summary}";
        }
        finally { IsScanning = false; }
    }

    [RelayCommand]
    private void SelectAll()
    {
        var all = Groups.Where(g => g.Removable).All(g => g.IsChecked);
        foreach (var g in Groups.Where(g => g.Removable)) g.IsChecked = !all;
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private Task RemoveSelected() => _main.RemoveDetectedAsync(Groups.Where(g => g.IsChecked && g.Removable).Select(g => g.Mod).ToList());
    private bool CanRemove() => Groups.Any(g => g.IsChecked && g.Removable);

    public static string FormatSize(long b) => b switch
    {
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB",
        _ => $"{b / (1024.0 * 1024 * 1024):F2} GB",
    };
}

/// <summary>Settings actions provided by the main window model.</summary>
public sealed record SettingsActions(
    Func<SettingsVm, string, Task> ChangeGame,
    Action<bool> SetReduceMotion,
    Func<SettingsVm, string, Task> SaveNexusKey,
    Action ForgetNexusKey,
    Func<SettingsVm, Task> CheckNexus,
    Func<SettingsVm, bool, Task> SetNxmHandler,
    Action OpenGame,
    Action OpenScan,
    Func<SettingsVm, Task> LoadStorage,
    Func<SettingsVm, string, Task> Purge,
    Action CopyDiagnostics,
    Func<SettingsVm, Task> RemoveFromPc);

/// <summary>P26: game, library, Nexus Mods (P24), display, diagnostics.</summary>
public sealed partial class SettingsVm : OverlayVm
{
    private readonly SettingsActions _a;
    private bool _loading = true;

    public SettingsVm(string gameFolder, string gameVersion, string library, bool reduceMotion, bool hasKey, bool nxmRegistered, string? otherNxmHandler, SettingsActions actions)
    {
        _gameFolder = gameFolder;
        GameVersion = gameVersion;
        Library = library;
        _reduceMotion = reduceMotion;
        _hasNexusKey = hasKey;
        _nxmHandler = nxmRegistered;
        OtherNxmHandler = otherNxmHandler;
        _a = actions;
        _nexusStatus = hasKey ? "Key saved · checking account…" : "No key yet";
        _loading = false;
    }

    public override string Title => "SETTINGS";
    public string Library { get; }
    public string GameVersion { get; }
    public string GameFolderHint => "To use another install: switch to Mods off and apply first. If you only moved the same install (new drive letter, new disk), AFTERLIFE recognizes its files and keeps them.";
    public string NexusHint => "Your personal API key from your Nexus Mods account (Settings → API Keys). It is stored encrypted for your Windows user and only sent to api.nexusmods.com.";
    public string? OtherNxmHandler { get; }
    public string NxmHint => OtherNxmHandler is null
        ? "Mod Manager Download on nexusmods.com then sends the mod straight into the AFTERLIFE library."
        : $"Now handled by {OtherNxmHandler}. AFTERLIFE remembers it and gives the links back when you switch this off.";

    [ObservableProperty] private string _gameFolder;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private Tone _messageTone;
    [ObservableProperty] private bool _reduceMotion;
    [ObservableProperty] private bool _busy;

    [ObservableProperty] private string _nexusKeyInput = "";
    [ObservableProperty] private bool _hasNexusKey;
    [ObservableProperty] private string _nexusStatus;
    [ObservableProperty] private Tone _nexusTone = Tone.Neutral;
    [ObservableProperty] private string? _nexusTier;
    [ObservableProperty] private bool _nxmHandler;

    partial void OnReduceMotionChanged(bool value) => _a.SetReduceMotion(value);

    partial void OnNxmHandlerChanged(bool value)
    {
        if (!_loading) _ = _a.SetNxmHandler(this, value);
    }

    public void SetNxmSilently(bool value)
    {
        _loading = true;
        NxmHandler = value;
        _loading = false;
    }

    [RelayCommand]
    private async Task ChangeGame()
    {
        var d = new Microsoft.Win32.OpenFolderDialog { Title = "Choose the Cyberpunk 2077 folder" };
        if (Directory.Exists(GameFolder)) d.InitialDirectory = GameFolder;
        if (d.ShowDialog() != true) return;
        Busy = true;
        try { await _a.ChangeGame(this, d.FolderName); }
        finally { Busy = false; }
    }

    [RelayCommand]
    private async Task SaveKey()
    {
        if (string.IsNullOrWhiteSpace(NexusKeyInput)) return;
        Busy = true;
        try { await _a.SaveNexusKey(this, NexusKeyInput.Trim()); NexusKeyInput = ""; }
        finally { Busy = false; }
    }

    /// <summary>Nexus Mods single sign-on; shown only once Nexus Mods has issued the application slug.</summary>
    public bool SsoAvailable => NexusSso.Available;

    [RelayCommand]
    private async Task LoginNexus()
    {
        Busy = true;
        NexusStatus = "Approve AFTERLIFE in the browser window that opens…";
        NexusTone = Tone.Info;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var key = await NexusSso.LoginAsync(AppLinks.Open, cts.Token);
            await _a.SaveNexusKey(this, key);
        }
        catch (Exception ex) when (ex is NexusException or OperationCanceledException or System.Net.WebSockets.WebSocketException or System.Text.Json.JsonException)
        {
            NexusStatus = ex is OperationCanceledException ? "Login timed out · try again or paste your key" : "Login failed: " + ex.Message;
            NexusTone = Tone.Block;
        }
        finally { Busy = false; }
    }

    [RelayCommand]
    private void ForgetKey()
    {
        _a.ForgetNexusKey();
        HasNexusKey = false;
        NexusTier = null;
        NexusStatus = "Key removed from this PC";
        NexusTone = Tone.Neutral;
    }

    // ---------------- storage (P57) ----------------
    [ObservableProperty] private string _removedSize = "…";
    [ObservableProperty] private string _downloadSize = "…";
    [ObservableProperty] private string _tempSize = "…";
    [ObservableProperty] private bool _hasRemoved;
    [ObservableProperty] private bool _hasDownloads;
    [ObservableProperty] private bool _hasTemp;

    public void SetStorage(StorageInfo s)
    {
        RemovedSize = $"{s.RemovedBatches} removal{(s.RemovedBatches == 1 ? "" : "s")} · {Size(s.RemovedBytes)}";
        DownloadSize = $"{s.Downloads} archive{(s.Downloads == 1 ? "" : "s")} · {Size(s.DownloadBytes)}";
        TempSize = Size(s.TempBytes);
        HasRemoved = s.RemovedBytes > 0 || s.RemovedBatches > 0;
        HasDownloads = s.Downloads > 0;
        HasTemp = s.TempBytes > 0;
    }

    public static string Size(long b) => b switch
    {
        >= 1L << 30 => $"{b / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{b / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{b / 1024.0:0} KB",
        _ => $"{b} B",
    };

    public void LoadStorageLater() => _ = _a.LoadStorage(this);
    [RelayCommand] private Task PurgeRemoved() => _a.Purge(this, "removed");
    [RelayCommand] private Task PurgeDownloads() => _a.Purge(this, "downloads");
    [RelayCommand] private Task PurgeTemp() => _a.Purge(this, "temp");
    [RelayCommand] private Task PurgeAll() => _a.Purge(this, "all");
    [RelayCommand] private void CopyDiagnostics() => _a.CopyDiagnostics();
    [RelayCommand] private Task RemoveFromPc() => _a.RemoveFromPc(this);

    [RelayCommand] private Task CheckKey() => _a.CheckNexus(this);
    [RelayCommand] private void OpenKeyPage() => AppLinks.Open(NexusClient.KeyPage);
    [RelayCommand] private void OpenGame() => _a.OpenGame();
    [RelayCommand] private void OpenScan() => _a.OpenScan();
    [RelayCommand] private void OpenLibrary() { if (Directory.Exists(Library)) AppLinks.OpenFolder(Library); }
    [RelayCommand] private void OpenLogs() { Directory.CreateDirectory(App.LogDirectory); AppLinks.OpenFolder(App.LogDirectory); }
}

public sealed partial class ProfileChoiceVm : ObservableObject
{
    public ProfileChoiceVm(ProfileSummary p, bool chosen) { Profile = p; _isChecked = chosen; }
    public ProfileSummary Profile { get; }
    public string Name => Profile.Name;
    [ObservableProperty] private bool _isChecked;
}

/// <summary>P11: choose profiles for several mods at once.</summary>
public sealed partial class ProfilePickerVm : OverlayVm
{
    private readonly TaskCompletionSource<IReadOnlyList<string>?> _tcs = new();

    public ProfilePickerVm(string what, IReadOnlyList<ProfileSummary> profiles, string selectedId)
    {
        What = what;
        Profiles = profiles.Select(p => new ProfileChoiceVm(p, p.Id == selectedId)).ToList();
    }

    public override string Title => "ADD TO PROFILE";
    public string What { get; }
    public string Hint => $"{What} will be wanted in the checked profiles. The game changes only when you apply the profile.";
    public IReadOnlyList<ProfileChoiceVm> Profiles { get; }
    public Task<IReadOnlyList<string>?> Result => _tcs.Task;

    [RelayCommand] private void Confirm() => _tcs.TrySetResult(Profiles.Where(p => p.IsChecked).Select(p => p.Profile.Id).ToList());
    [RelayCommand] private void Cancel() => _tcs.TrySetResult(null);
}

public sealed class PlanPreviewVm : OverlayVm
{
    public PlanPreviewVm(string profile, PreviewResult r)
    {
        Profile = profile;
        Summary = r.Plan.IsNoop ? "Nothing to change. The game already matches this profile." : $"{r.Plan.Steps.Count} file change(s)";
        Steps = r.Plan.Steps.Select(s => new LineVm($"{s.Kind.ToString().ToUpperInvariant(),-8} {s.Path}", s.Reason,
            s.Kind == StepKind.Remove ? Tone.Warn : Tone.Info)).ToList();
        Issues = r.EnvironmentIssues.Concat(r.Plan.Blockers).Concat(r.Plan.Warnings)
            .Select(i => new LineVm(i.Message, i.Path ?? "", i.Severity == Severity.Blocking ? Tone.Block : Tone.Warn)).ToList();
    }

    public override string Title => "PREVIEW · " + Profile.ToUpperInvariant();
    public string Profile { get; }
    public string Summary { get; }
    public string SavesHint => "Saves made with a mod may not load without it. Switching profiles never touches your saves.";
    public IReadOnlyList<LineVm> Steps { get; }
    public IReadOnlyList<LineVm> Issues { get; }
}

public sealed class AboutVm : OverlayVm
{
    public override string Title => "ABOUT AFTERLIFE";
    public string Version => "Version " + (typeof(AboutVm).Assembly.GetName().Version?.ToString(3) ?? "0.5.0") + " · beta, unsigned";
    public string Copyright => "© 2026 Silentbobcz · MIT License · free & open source";
    public string Disclaimer => "This is an unofficial fan work and is not approved/endorsed by CD PROJEKT RED.";
    public string Trademarks => "Cyberpunk 2077 and related marks are trademarks of CD PROJEKT S.A. AFTERLIFE uses no game assets.";
    public string Contact => AppLinks.ContactEmail;
    public string License => Licenses.Mit;
    public IReadOnlyList<LineVm> Libraries { get; } =
    [
        new("SharpCompress", "MIT"),
        new("Microsoft.Data.Sqlite / SQLitePCLRaw", "MIT / Apache-2.0"),
        new("SQLite", "public domain"),
        new("CommunityToolkit.Mvvm", "MIT"),
        new("System.IO.Hashing", "MIT"),
        new("Chakra Petch font", "SIL OFL 1.1"),
        new("JetBrains Mono font", "SIL OFL 1.1"),
    ];
    public ICommand SourceCommand { get; } = new RelayCommand(() => AppLinks.Open(AppLinks.Source));
    public ICommand SupportCommand { get; } = new RelayCommand(() => AppLinks.Open(AppLinks.Sponsors));
    public ICommand ContactCommand { get; } = new RelayCommand(() => AppLinks.Open(AppLinks.ContactMailto));
    public ICommand FanContentCommand { get; } = new RelayCommand(() => AppLinks.Open(AppLinks.CdprFanContent));
}

public static class Licenses
{
    public const string Mit = """
        MIT License

        Copyright (c) 2026 Silentbobcz

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
        """;
}


/// <summary>P48: mods downloaded from Nexus Mods through AFTERLIFE, plus a link to the full history on the website.</summary>
public sealed partial class DownloadsVm(IReadOnlyList<DownloadVm> items) : OverlayVm
{
    public override string Title => "DOWNLOAD HISTORY";
    public IReadOnlyList<DownloadVm> All { get; } = items;
    /// <summary>P77: filtered by the search box (mod name and file).</summary>
    public IReadOnlyList<DownloadVm> Items => string.IsNullOrWhiteSpace(Search) ? All
        : All.Where(i => i.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase) || i.Detail.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Items), nameof(NoMatch))] private string _search = "";
    public bool HasItems => All.Count > 0;
    public bool NoMatch => All.Count > 0 && Items.Count == 0;
    public bool IsEmpty => All.Count == 0;
    public string Summary => All.Count == 0
        ? "Nothing downloaded through AFTERLIFE yet. Use Mod Manager Download on a mod page; it comes here."
        : $"{All.Count} download{(All.Count == 1 ? "" : "s")} through AFTERLIFE · newest first";
    [RelayCommand] private void OpenWebHistory() => AppLinks.Open(DownloadHistory.WebPage);
    [RelayCommand] private void OpenNexus() => AppLinks.Open(NexusClient.CyberpunkPage);
}

public sealed record DownloadVm(string Name, string Detail, string Status, Tone Tone, ICommand Open, ICommand? Reimport)
{
    public bool CanReimport => Reimport is not null;
}


/// <summary>P52: the list of active mods with versions, to copy or save when looking for a problem mod.</summary>
public sealed partial class ExportVm : OverlayVm
{
    private readonly Func<ExportScope, bool, string> _text;
    private readonly Func<ExportScope, bool, string> _csv;
    private readonly Action<string> _copy;

    public ExportVm(Func<ExportScope, bool, string> text, Func<ExportScope, bool, string> csv, Action<string> copy, bool hasPending)
    {
        _text = text;
        _csv = csv;
        _copy = copy;
        HasPending = hasPending;
        _preview = text(ExportScope.InGameNow, false);
    }

    public override string Title => "EXPORT MOD LIST";
    public string Hint => "Everything that is in the game now, with versions, in load order. Paste it on a forum, Discord or into a bug report. Your Nexus key and paths outside the game are never included.";
    public bool HasPending { get; }
    [ObservableProperty] private bool _afterApply;
    [ObservableProperty] private bool _includeOff;
    [ObservableProperty] private string _preview;
    [ObservableProperty] private string? _message;

    private ExportScope Scope => AfterApply ? ExportScope.SelectedAfterApply : ExportScope.InGameNow;
    partial void OnAfterApplyChanged(bool value) => Preview = _text(Scope, IncludeOff);
    partial void OnIncludeOffChanged(bool value) => Preview = _text(Scope, IncludeOff);

    [RelayCommand] private void Copy() => _copy(Preview);
    [RelayCommand] private void SaveText() => Save("txt", "Text file|*.txt", Preview);
    [RelayCommand] private void SaveCsv() => Save("csv", "CSV for Excel|*.csv", _csv(Scope, IncludeOff));

    private void Save(string ext, string filter, string content)
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = $"afterlife-mods-{DateTime.Now:yyyy-MM-dd-HHmm}.{ext}" };
        if (d.ShowDialog() != true) return;
        try { File.WriteAllText(d.FileName, content, new System.Text.UTF8Encoding(true)); Message = "Saved: " + d.FileName; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Message = "Could not save: " + ex.Message; }
    }
}

/// <summary>R3: the mod's own installer (FOMOD), step by step, in AFTERLIFE's look. Returns the choices or null.</summary>
public sealed partial class FomodVm : OverlayVm
{
    private readonly TaskCompletionSource<IReadOnlyList<FomodChoice>?> _tcs = new();
    private readonly FomodConfig _config;
    private readonly Func<string, byte[]?> _image;
    private readonly Dictionary<(string Step, string Group), HashSet<string>> _picked = new();

    public FomodVm(string fileName, FomodConfig config, Func<string, byte[]?> image, IReadOnlyList<FomodChoice>? initial = null)
    {
        FileName = fileName;
        _config = config;
        _image = image;
        foreach (var s in config.Steps)
            foreach (var g in s.Groups)
            {
                var c = initial?.FirstOrDefault(x => string.Equals(x.Step, s.Name, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Group, g.Name, StringComparison.OrdinalIgnoreCase));
                var names = c is null ? FomodConfig.Defaults(g).Select(p => p.Name) : c.Plugins;
                _picked[(s.Name, g.Name)] = new HashSet<string>(names.Concat(g.Plugins.Where(p => p.Type == FomodPluginType.Required).Select(p => p.Name)), StringComparer.OrdinalIgnoreCase);
            }
        Build();
    }

    public override string Title => "INSTALL OPTIONS";
    public string FileName { get; }
    public string ModName => _config.Name;
    public Task<IReadOnlyList<FomodChoice>?> Result => _tcs.Task;

    [ObservableProperty] private int _index;
    [ObservableProperty] private string _stepName = "";
    [ObservableProperty] private string _stepCounter = "";
    [ObservableProperty] private IReadOnlyList<FomodGroupVm> _groups = [];
    [ObservableProperty] private string? _description;
    [ObservableProperty] private System.Windows.Media.ImageSource? _picture;
    [ObservableProperty] private string? _problem;
    public bool CanBack => Index > 0;
    public bool IsLast => Index >= Steps.Count - 1;
    public string NextLabel => IsLast ? "INSTALL" : "NEXT";

    private IReadOnlyList<FomodStep> Steps => _config.VisibleSteps(Choices());

    public IReadOnlyList<FomodChoice> Choices() =>
        _config.Steps.SelectMany(s => s.Groups.Select(g => new FomodChoice(s.Name, g.Name,
            g.Plugins.Where(p => _picked[(s.Name, g.Name)].Contains(p.Name)).Select(p => p.Name).ToList()))).ToList();

    private void Build()
    {
        var steps = Steps;
        if (steps.Count == 0) { StepName = "No options"; Groups = []; return; }
        if (Index >= steps.Count) Index = steps.Count - 1;
        var step = steps[Index];
        StepName = step.Name;
        StepCounter = $"STEP {Index + 1} / {steps.Count}";
        Groups = step.Groups.Select(g => new FomodGroupVm(this, step.Name, g)).ToList();
        OnPropertyChanged(nameof(CanBack));
        OnPropertyChanged(nameof(IsLast));
        OnPropertyChanged(nameof(NextLabel));
        var first = step.Groups.SelectMany(g => g.Plugins).FirstOrDefault(p => _picked.Any(k => k.Key.Step == step.Name && k.Value.Contains(p.Name)))
                    ?? step.Groups.SelectMany(g => g.Plugins).FirstOrDefault();
        Show(first);
        Problem = null;
    }

    internal bool IsPicked(string step, string group, string plugin) => _picked[(step, group)].Contains(plugin);

    internal void Pick(string step, FomodGroup g, FomodPlugin p, bool on)
    {
        var set = _picked[(step, g.Name)];
        if (p.Type == FomodPluginType.Required && !on) return;
        if (g.Type is FomodGroupType.SelectExactlyOne or FomodGroupType.SelectAtMostOne)
        {
            if (on) { set.Clear(); set.Add(p.Name); }
            else if (g.Type == FomodGroupType.SelectAtMostOne) set.Remove(p.Name);
        }
        else if (on) set.Add(p.Name); else set.Remove(p.Name);
        Show(p);
        foreach (var gv in Groups) gv.Refresh();
    }

    internal void Show(FomodPlugin? p)
    {
        Description = p is null ? null : string.IsNullOrWhiteSpace(p.Description) ? p.Name : p.Description;
        Picture = null;
        var path = p?.Image ?? _config.Image;
        if (path is null) return;
        try
        {
            var bytes = _image(path);
            if (bytes is null) return;
            var img = new System.Windows.Media.Imaging.BitmapImage();
            img.BeginInit();
            img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            img.StreamSource = new MemoryStream(bytes);
            img.DecodePixelWidth = 520;
            img.EndInit();
            img.Freeze();
            Picture = img;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException or InvalidOperationException) { }
    }

    private string? Validate()
    {
        var steps = Steps;
        if (steps.Count == 0) return null;
        var step = steps[Index];
        foreach (var g in step.Groups)
        {
            var n = _picked[(step.Name, g.Name)].Count;
            if (g.Type == FomodGroupType.SelectExactlyOne && n != 1) return $"Choose one option in \"{g.Name}\".";
            if (g.Type == FomodGroupType.SelectAtLeastOne && n < 1) return $"Choose at least one option in \"{g.Name}\".";
        }
        return null;
    }

    [RelayCommand]
    private void Next()
    {
        Problem = Validate();
        if (Problem is not null) return;
        if (IsLast) { _tcs.TrySetResult(Choices()); return; }
        Index++;
        Build();
    }

    [RelayCommand]
    private void Back()
    {
        if (Index == 0) return;
        Index--;
        Build();
    }

    [RelayCommand]
    private void Cancel() => _tcs.TrySetResult(null);
}

public sealed partial class FomodGroupVm : ObservableObject
{
    public FomodGroupVm(FomodVm owner, string step, FomodGroup g)
    {
        Name = g.Name;
        Rule = g.Type switch
        {
            FomodGroupType.SelectExactlyOne => "choose one",
            FomodGroupType.SelectAtMostOne => "choose one or none",
            FomodGroupType.SelectAtLeastOne => "choose at least one",
            FomodGroupType.SelectAll => "all included",
            _ => "choose any",
        };
        var radio = g.Type is FomodGroupType.SelectExactlyOne or FomodGroupType.SelectAtMostOne;
        Options = g.Plugins.Select(p => new FomodOptionVm(owner, step, g, p, radio)).ToList();
    }

    public string Name { get; }
    public string Rule { get; }
    public IReadOnlyList<FomodOptionVm> Options { get; }
    public void Refresh() { foreach (var o in Options) o.Refresh(); }
}

public sealed partial class FomodOptionVm(FomodVm owner, string step, FomodGroup group, FomodPlugin plugin, bool radio) : ObservableObject
{
    public string Name => plugin.Name;
    public bool IsRadio => radio;
    public bool IsCheck => !radio;
    public string GroupKey => step + "|" + group.Name;
    public bool IsEnabled => plugin.Type is not (FomodPluginType.NotUsable or FomodPluginType.Required) && group.Type != FomodGroupType.SelectAll;
    public string? Badge => plugin.Type switch
    {
        FomodPluginType.Recommended => "RECOMMENDED",
        FomodPluginType.Required => "REQUIRED",
        FomodPluginType.NotUsable => "NOT USABLE",
        _ => null,
    };

    public bool IsChecked
    {
        get => owner.IsPicked(step, group.Name, plugin.Name);
        set { owner.Pick(step, group, plugin, value); OnPropertyChanged(); }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsChecked));

    [RelayCommand] private void Hover() => owner.Show(plugin);
}

/// <summary>P51: what a collection contains compared with the library, before it is added.</summary>
public sealed partial class CollectionPreviewVm : OverlayVm
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public CollectionPreviewVm(CollectionRevisionInfo rev, IReadOnlyList<CollectionItemState> states, string? gameVersion, bool isPremium, bool update = false)
    {
        Rev = rev;
        Required = states.Where(s => !s.Item.Optional).Select(s => new CollectionItemVm(s, false)).ToList();
        Optional = states.Where(s => s.Item.Optional).Select(s => new CollectionItemVm(s, true)).ToList();
        var missing = states.Count(s => s.Status is ItemStatus.Missing or ItemStatus.OtherVersion && !s.Item.Optional);
        Header = $"{rev.Name} · revision {rev.Revision}{(rev.Curator is null ? "" : " · by " + rev.Curator)} · {states.Count} mods · {SettingsVm.Size(rev.TotalSize)}";
        Summary = rev.Summary;
        Instructions = string.IsNullOrWhiteSpace(rev.Instructions) ? null : rev.Instructions;
        DownloadNote = missing == 0 ? "Everything required is already in your library."
            : isPremium ? $"{missing} required mod(s) will be downloaded one after another (Premium)."
            : $"{missing} required mod(s) to download: AFTERLIFE opens each page, you click Mod Manager Download, it moves on to the next.";
        Warning = rev.LatestRevision is { } l && l > rev.Revision ? $"A newer revision ({l}) exists." : null;
        if (!rev.FromBundle) Warning = (Warning is null ? "" : Warning + " ") + "The collection's installer choices and load rules could not be read; mods install with their defaults.";
        Adult = rev.Adult;
        IsUpdate = update;
        _newProfile = !update;
        GameVersion = gameVersion;
    }

    public CollectionRevisionInfo Rev { get; }
    public override string Title => IsUpdate ? "UPDATE COLLECTION" : "ADD COLLECTION";
    public string Header { get; }
    public string? Summary { get; }
    public string? Instructions { get; }
    public bool HasInstructions => Instructions is not null;
    public string DownloadNote { get; }
    public string? Warning { get; }
    public bool Adult { get; }
    public bool IsUpdate { get; }
    public bool ShowTarget => !IsUpdate;
    public string? GameVersion { get; }
    public IReadOnlyList<CollectionItemVm> Required { get; }
    public IReadOnlyList<CollectionItemVm> Optional { get; }
    public bool HasOptional => Optional.Count > 0;
    [ObservableProperty] private bool _newProfile;
    [ObservableProperty] private bool _showInstructions;
    public string ProfileLabel => $"New profile \"{Rev.Name}\" (recommended: exactly what the curator tested)";
    public Task<bool> Result => _tcs.Task;
    public IReadOnlyList<string> ChosenOptional => Optional.Where(o => o.IsChecked).Select(o => o.State.Item.Key).ToList();

    [RelayCommand] private void Confirm() => _tcs.TrySetResult(true);
    [RelayCommand] private void Cancel() => _tcs.TrySetResult(false);
    [RelayCommand] private void ToggleInstructions() => ShowInstructions = !ShowInstructions;
}

public sealed partial class CollectionItemVm(CollectionItemState s, bool optional) : ObservableObject
{
    public CollectionItemState State => s;
    public string Name => s.Item.Name;
    public string Version => s.Item.Version ?? "";
    public string Status => s.Label;
    public Tone Tone => s.Status switch
    {
        ItemStatus.InLibrary => Tone.Ok,
        ItemStatus.Bundled => Tone.Info,
        ItemStatus.OutsideNexus => Tone.Warn,
        _ => optional ? Tone.Neutral : Tone.Warn,
    };
    public bool IsOptional => optional;
    [ObservableProperty] private bool _isChecked;
    public ICommand Open => new RelayCommand(() => { if (s.Item.PageUrl is { } u) AppLinks.Open(u); });
}

/// <summary>P51: one collection in the library: progress, update, order, instructions, remove.</summary>
public sealed partial class ManageCollectionVm : OverlayVm
{
    public ManageCollectionVm(CollectionRecord r, IReadOnlyList<CollectionItemState> states, MainViewModel main)
    {
        Record = r;
        _headerBase = $"revision {r.Revision.Revision}{(r.Revision.Curator is null ? "" : " · by " + r.Revision.Curator)}";
        _headerTail = r.Revision.LatestRevision is { } l && l > r.Revision.Revision ? $" · revision {l} available" : "";
        _inLibrary = r.Progress;
        Items = states.Where(s => !s.Item.Optional || r.ChosenOptional.Contains(s.Item.Key))
            .Select(s => new CollectionItemVm(r.PackageOf.ContainsKey(s.Item.Key) ? s with { Status = ItemStatus.InLibrary, Label = "in library" } : s, s.Item.Optional)).ToList();
        Missing = r.Missing.Count;
        ContinueCommand = new AsyncRelayCommand(() => main.ContinueCollectionAsync(r.Slug));
        UpdateCommand = new AsyncRelayCommand(() => main.UpdateCollectionAsync(r.Slug));
        RemoveCommand = new AsyncRelayCommand(() => main.RemoveCollectionAsync(r.Slug));
        LockCommand = new AsyncRelayCommand(() => main.SetCollectionLockAsync(r.Slug, !r.OrderLocked));
        ResetOrderCommand = new AsyncRelayCommand(() => main.ApplyCollectionOrderAsync(r.Slug, true));
        OpenPageCommand = new RelayCommand(() => AppLinks.Open($"https://www.nexusmods.com/games/{NxmLink.Cyberpunk}/collections/{r.Slug}"));
    }

    public CollectionRecord Record { get; }
    /// <summary>P69: the running (or paused) download of this collection, if any.</summary>
    public CollectionProgressVm? Progress { get; init; }
    public bool HasProgress => Progress is not null;
    public bool ShowContinue => HasMissing && Progress is null;
    public override string Title => "COLLECTION · " + Record.Name.ToUpperInvariant();
    private readonly string _headerBase, _headerTail, _inLibrary;
    /// <summary>P79: while downloading, the count is in the progress block (it stays current there).</summary>
    public string Header => _headerBase + (Progress is null ? $" · {_inLibrary} in library" : "") + _headerTail;
    public string? Instructions => string.IsNullOrWhiteSpace(Record.Revision.Instructions) ? null : Record.Revision.Instructions;
    public bool HasInstructions => Instructions is not null;
    public IReadOnlyList<CollectionItemVm> Items { get; }
    public int Missing { get; }
    public bool HasMissing => Missing > 0;
    public string ContinueLabel => $"DOWNLOAD MISSING ({Missing})";
    public string LockLabel => Record.OrderLocked ? "Unlock load order…" : "Lock load order";
    public string LockState => Record.OrderLocked ? "Load order is set by the collection (locked)." : "Load order is unlocked: you can drag its mods.";
    public ICommand ContinueCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand LockCommand { get; }
    public ICommand ResetOrderCommand { get; }
    public ICommand OpenPageCommand { get; }
}

/// <summary>P69/P70: download of a collection, shown in its window: overall bar, current file bar, items, pause and resume.</summary>
public sealed partial class CollectionProgressVm : ObservableObject
{
    public CollectionProgressVm(string slug, string name) { Slug = slug; Name = name; }
    public string Slug { get; }
    public string Name { get; }
    public ObservableCollection<CollectionProgressItemVm> Items { get; } = new();

    [ObservableProperty, NotifyPropertyChangedFor(nameof(OverallPercent), nameof(CountText))] private int _total;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(OverallPercent), nameof(CountText))] private int _done;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CountText))] private int _failed;
    [ObservableProperty] private string? _current;
    [ObservableProperty] private double _currentPercent;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanPause), nameof(CanResume), nameof(Tone), nameof(StateLabel))] private bool _isRunning;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanPause), nameof(CanResume), nameof(Tone), nameof(StateLabel))] private bool _isPaused;
    [ObservableProperty] private string _statusText = "";

    /// <summary>Set by Pause; the loop stops after the file in progress.</summary>
    public bool PauseRequested { get; set; }
    /// <summary>The user closed the window: the download goes on, the window stays closed.</summary>
    public bool Hidden { get; set; }

    public double OverallPercent => Total == 0 ? 0 : Done * 100.0 / Total;
    /// <summary>P79: the file in progress as one line: name, and the percent when AFTERLIFE downloads it itself (Premium).</summary>
    public string? CurrentLine => Current is null ? null : CurrentPercent > 0 && CurrentPercent < 100 ? $"> {Current} · {CurrentPercent:0} %" : $"> {Current}";
    partial void OnCurrentChanged(string? value) => OnPropertyChanged(nameof(CurrentLine));
    partial void OnCurrentPercentChanged(double value) => OnPropertyChanged(nameof(CurrentLine));
    public string CountText => $"{Done} / {Total} MODS · {OverallPercent:0} %" + (Failed > 0 ? $" · {Failed} FAILED" : "");
    public bool CanPause => IsRunning && !IsPaused;
    public bool CanResume => IsPaused || (!IsRunning && Done < Total);
    public Tone Tone => IsPaused ? Tone.Warn : Failed > 0 && !IsRunning ? Tone.Block : Done >= Total && Total > 0 ? Tone.Ok : Tone.Info;
    public string StateLabel => IsPaused ? "PAUSED" : IsRunning ? "DOWNLOADING" : Done >= Total && Total > 0 ? "COMPLETE" : "STOPPED";

    public ICommand? PauseCommand { get; init; }
    public ICommand? ResumeCommand { get; init; }

    public CollectionProgressItemVm? Item(string key) => Items.FirstOrDefault(i => i.Key == key);
}

public sealed partial class CollectionProgressItemVm(string key, string name) : ObservableObject
{
    public string Key { get; } = key;
    public string Name { get; } = name;
    [ObservableProperty] private string _status = "waiting";
    [ObservableProperty] private Tone _tone = Tone.Neutral;
}
