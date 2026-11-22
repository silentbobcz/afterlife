using System.IO;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>The tabs on the left of the settings, in their order.</summary>
public enum SettingsTab { Game, Nexus, Updates, Display, Storage, Diagnostics, Fixer }

/// <summary>Which tab is shown, the short facts on the tabs, and the folders behind the kept files.</summary>
public sealed partial class SettingsVm
{
    /// <summary>The tab chosen last in this run; the settings open on it again.</summary>
    private static SettingsTab s_lastTab = SettingsTab.Game;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameTab), nameof(IsNexusTab), nameof(IsUpdatesTab), nameof(IsDisplayTab),
        nameof(IsStorageTab), nameof(IsDiagnosticsTab), nameof(IsFixerTab))]
    private SettingsTab _tab = s_lastTab;

    partial void OnTabChanged(SettingsTab value) => s_lastTab = value;

    [RelayCommand] private void SelectTab(SettingsTab tab) => Tab = tab;

    public bool IsGameTab => Tab == SettingsTab.Game;
    public bool IsNexusTab => Tab == SettingsTab.Nexus;
    public bool IsUpdatesTab => Tab == SettingsTab.Updates;
    public bool IsDisplayTab => Tab == SettingsTab.Display;
    public bool IsStorageTab => Tab == SettingsTab.Storage;
    public bool IsDiagnosticsTab => Tab == SettingsTab.Diagnostics;
    public bool IsFixerTab => Tab == SettingsTab.Fixer;

    /// <summary>Green dot on the Nexus Mods tab: the saved key works.</summary>
    public bool NexusKeyWorks => IsOnline && NexusTone == Tone.Ok;
    partial void OnNexusToneChanged(Tone value) => OnPropertyChanged(nameof(NexusKeyWorks));

    /// <summary>All kept files together, shown on the Kept files tab; empty until measured.</summary>
    [ObservableProperty] private string? _keptTotal;

    public string VersionText => $"AFTERLIFE {AppInfo.Version}{(IsOffline ? " · Offline" : "")}";

    public string FixerAbout => "FIXER is a quest editor for Cyberpunk 2077: you build a quest on a map of scenes, calls and journal entries, and it comes out as a mod. "
        + "It is made by the author of AFTERLIFE and is still being worked on; until it is out, this switch has nothing to receive.";

    private LibraryPaths Paths => new(Library);

    [RelayCommand] private void ShowRemoved() => ShowFolder(Paths.Removed);
    [RelayCommand] private void ShowDownloads() => ShowFolder(Paths.Downloads);
    [RelayCommand] private void ShowTemp() => ShowFolder(Paths.Staging);

    /// <summary>Opens a kept-files folder; one that does not exist yet opens the library instead.</summary>
    private void ShowFolder(string folder)
    {
        if (Directory.Exists(folder)) AppLinks.OpenFolder(folder);
        else if (Directory.Exists(Library)) AppLinks.OpenFolder(Library);
    }
}
