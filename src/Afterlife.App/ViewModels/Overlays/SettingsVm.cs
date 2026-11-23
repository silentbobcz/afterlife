using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>

/// <summary>Settings actions provided by the main window model.</summary>
public sealed record SettingsActions(
    Func<SettingsVm, string, Task> ChangeGame,
    Action<bool> SetReduceMotion,
    Action<bool> SetActivityLog,
    Func<SettingsVm, string, Task> SaveNexusKey,
    Action ForgetNexusKey,
    Func<SettingsVm, Task> CheckNexus,
    Func<SettingsVm, bool, Task> SetNxmHandler,
    Action OpenGame,
    Action OpenScan,
    Func<SettingsVm, Task> LoadStorage,
    Func<SettingsVm, string, Task> Purge,
    Action CopyDiagnostics,
    Func<SettingsVm, Task> RemoveFromPc,
    Func<SettingsVm, bool, Task> SetFixer,
    Action LinkMods,
    Action<bool> SetVirtualizeList,
    Action<bool, bool, bool, int> SetUpdateChecks,
    Action CheckAllMods,
    Func<SettingsVm, string, Task> MoveLibrary);

/// <summary>Settings in tabs: game and library, Nexus Mods, updates, display, kept files, diagnostics, FIXER.</summary>
public sealed partial class SettingsVm : OverlayVm
{
    private readonly SettingsActions _a;
    private bool _loading = true;

    public SettingsVm(string gameFolder, string gameVersion, string library, bool reduceMotion, bool activityLog, bool hasKey, bool nxmRegistered, string? otherNxmHandler, SettingsActions actions)
    {
        _gameFolder = gameFolder;
        GameVersion = gameVersion;
        Library = library;
        _reduceMotion = reduceMotion;
        _activityLog = activityLog;
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
    public bool IsOffline => AppEdition.Offline;
    public bool IsOnline => !AppEdition.Offline;
    public string OfflineNote => $"This is AFTERLIFE Offline: it does not connect to Nexus Mods and needs no API key. Downloads, updates and collections from Nexus Mods come once Nexus Mods registers AFTERLIFE (requested {AppEdition.RegistrationRequested}). Until then download mods on nexusmods.com with Manual download and drop them into the window.";
    public string NexusHint => "Your personal API key from your Nexus Mods account (Settings → API Keys). It is stored encrypted for your Windows user and only sent to api.nexusmods.com.";
    public string? OtherNxmHandler { get; }
    public string NxmHint => OtherNxmHandler is null
        ? "Mod Manager Download on nexusmods.com then sends the mod straight into the AFTERLIFE library."
        : $"Now handled by {OtherNxmHandler}. AFTERLIFE remembers it and gives the links back when you switch this off.";

    [ObservableProperty] private string _gameFolder;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private Tone _messageTone;
    [ObservableProperty] private bool _reduceMotion;
    [ObservableProperty] private bool _activityLog;
    [ObservableProperty] private bool _busy;

    [ObservableProperty] private string _nexusKeyInput = "";
    [ObservableProperty] private bool _hasNexusKey;
    [ObservableProperty] private string _nexusStatus;
    [ObservableProperty] private Tone _nexusTone = Tone.Neutral;
    [ObservableProperty] private string? _nexusTier;
    [ObservableProperty] private bool _nxmHandler;

    partial void OnReduceMotionChanged(bool value) => _a.SetReduceMotion(value);

    /// <summary>Only the visible rows of the mod list are drawn (off by default until it has proven itself).</summary>
    [ObservableProperty] private bool _virtualizeList;

    partial void OnVirtualizeListChanged(bool value)
    {
        if (!_loading) _a.SetVirtualizeList(value);
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(AnyAutoCheck))] private bool _checkUpdatesAuto = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AnyAutoCheck), nameof(UpdatesLoad), nameof(UpdatesWarning), nameof(HasUpdatesWarning))] private bool _checkUpdatesHourly = true;
    /// <summary>Some check runs on its own, so its scope can be chosen.</summary>
    public bool AnyAutoCheck => CheckUpdatesAuto || CheckUpdatesHourly;
    [ObservableProperty] private bool _checkUpdatesActiveOnly;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(UpdatesLoad), nameof(UpdatesWarning), nameof(HasUpdatesWarning))] private int _fullCheckDays = 7;

    /// <summary>Mods linked to Nexus Mods: what the update check has to ask about.</summary>
    public int LinkedMods { get; private set; }
    public string UpdatesLoad => $"With your {LinkedMods:N0} linked mod(s) this costs about {NexusLimits.RequestsPerDay(LinkedMods, FullCheckDays, CheckUpdatesHourly):N0} requests a day. "
        + $"Nexus Mods allows {NexusLimits.PerDay:N0} a day and {NexusLimits.PerHour} an hour per key; when it is reached AFTERLIFE waits and goes on later.";
    /// <summary>The days between full reads are too few for this library.</summary>
    public string? UpdatesWarning => NexusLimits.SettingsWarning(LinkedMods, FullCheckDays, CheckUpdatesHourly);
    public bool HasUpdatesWarning => UpdatesWarning is not null;
    public string CheckAllTip => LinkedMods > NexusLimits.PerHour - NexusLimits.HourlyReserve
        ? $"Reads the file list of every linked mod: {LinkedMods:N0} requests, more than Nexus Mods allows in one hour ({NexusLimits.PerHour}). It will pause at the limit and finish by itself over the next hours."
        : "Reads the file list of every linked mod: one request to Nexus Mods per mod. CHECK UPDATES in the main window reads only the mods that changed.";
    public string UpdatesHint => "Between full reads AFTERLIFE asks Nexus Mods only which mods changed: one request for the whole library. The full read is a safety net; the mods take turns, so about the same number is read every day.";

    partial void OnCheckUpdatesAutoChanged(bool value) => SaveUpdateChecks();
    partial void OnCheckUpdatesHourlyChanged(bool value) => SaveUpdateChecks();
    partial void OnCheckUpdatesActiveOnlyChanged(bool value) => SaveUpdateChecks();

    partial void OnFullCheckDaysChanged(int value)
    {
        if (value is < 0 or > 365) { FullCheckDays = Math.Clamp(value, 0, 365); return; }
        SaveUpdateChecks();
    }

    private void SaveUpdateChecks()
    {
        if (!_loading) _a.SetUpdateChecks(CheckUpdatesAuto, CheckUpdatesHourly, CheckUpdatesActiveOnly, FullCheckDays);
    }

    public void SetUpdateChecksSilently(bool atStart, bool hourly, bool activeOnly, int days, int linkedMods)
    {
        _loading = true;
        LinkedMods = linkedMods;
        CheckUpdatesAuto = atStart;
        CheckUpdatesHourly = hourly;
        CheckUpdatesActiveOnly = activeOnly;
        FullCheckDays = Math.Clamp(days, 0, 365);
        _loading = false;
    }

    /// <summary>Reads every linked mod on Nexus Mods now, one request per mod.</summary>
    [RelayCommand] private void CheckAllMods() => _a.CheckAllMods();

    public void SetVirtualizeListSilently(bool value)
    {
        _loading = true;
        VirtualizeList = value;
        _loading = false;
    }

    partial void OnActivityLogChanged(bool value) => _a.SetActivityLog(value);
    public string ActivityLogHint => "Writes what AFTERLIFE does (switches, imports, every planned file change with its reason, results) into the log folder, one file per day. Switch it on when something looks wrong, then send the file with your report.";

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

    // ---------------- FIXER ----------------
    [ObservableProperty] private bool _fixerInbox;

    public string FixerFolder => Path.Combine(Library, "fixer", "inbox");
    public string FixerHint => "While this is on, AFTERLIFE takes the quests FIXER leaves in the fixer\\inbox folder of the library: "
        + "they arrive in the category FIXER, switched on in the selected profile, and go into the game with APPLY CHANGES like any other mod. Off: the folder is not read.";

    partial void OnFixerInboxChanged(bool value)
    {
        if (!_loading) _ = _a.SetFixer(this, value);
    }

    public void SetFixerSilently(bool value)
    {
        _loading = true;
        FixerInbox = value;
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

    public string LibraryHint => "MOVE… takes the whole library to another folder or drive. The game is not touched; mods in the game keep working.";

    [RelayCommand]
    private async Task MoveLibrary()
    {
        var d = new Microsoft.Win32.OpenFolderDialog { Title = "Choose the new place for the library (an empty or new folder)" };
        if (d.ShowDialog() != true) return;
        Busy = true;
        try { await _a.MoveLibrary(this, d.FolderName); }
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

    // ---------------- storage ----------------
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
        KeptTotal = Size(s.RemovedBytes + s.DownloadBytes + s.TempBytes);
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
    /// <summary>Links every mod added from a file to its Nexus page, the same as in the details of a mod.</summary>
    [RelayCommand] private void LinkMods() => _a.LinkMods();
    [RelayCommand] private void OpenLibrary() { if (Directory.Exists(Library)) AppLinks.OpenFolder(Library); }
    [RelayCommand] private void OpenLogs() { Directory.CreateDirectory(App.LogDirectory); AppLinks.OpenFolder(App.LogDirectory); }
}
