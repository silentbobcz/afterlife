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
    Func<SettingsVm, Task> RemoveFromPc);

/// <summary>Game, library, Nexus Mods, display, diagnostics.</summary>
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
