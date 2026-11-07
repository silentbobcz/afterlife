using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>

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
    /// <summary>Optional personal Nexus Mods API key, asked together with the folders.</summary>
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

    /// <summary>Library next to AFTERLIFE.exe when that place is usable, otherwise %LOCALAPPDATA%.</summary>
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
        // Simple check of the default store locations; full detection (Steam libraries, GOG, Epic) is milestone.
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
