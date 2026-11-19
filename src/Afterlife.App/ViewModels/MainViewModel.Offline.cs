using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>AFTERLIFE Offline: the online features stay visible with a lock and open an explanation instead.</summary>
public sealed partial class MainViewModel
{
    public bool IsOffline => AppEdition.Offline;

    public string WindowTitle => AppEdition.Offline ? "AFTERLIFE Offline · mod manager for Cyberpunk 2077" : "AFTERLIFE · mod manager for Cyberpunk 2077";

    public string LockedNote => AppEdition.LockedNote;

    [RelayCommand]
    private void OpenOfflineInfo() => Overlay = new OfflineInfoVm { CloseCommand = new RelayCommand(() => Overlay = null) };

    /// <summary>The badge in the header: the explanation in the Offline edition, the Nexus settings otherwise.</summary>
    [RelayCommand]
    private void OpenNexusStatus()
    {
        if (AppEdition.Offline) OpenOfflineInfo();
        else OpenSettings();
    }

    /// <summary>
    /// Call at the start of every feature that needs the Nexus Mods API. In the Offline edition it explains why the
    /// feature is locked and returns true; the caller then stops.
    /// </summary>
    private bool LockedOffline(string feature)
    {
        if (!AppEdition.Offline) return false;
        Say($"{feature} comes with the online features: AFTERLIFE Offline waits for Nexus Mods to register it.", Tone.Warn);
        OpenOfflineInfo();
        return true;
    }

    private void ShowOfflineBadge()
    {
        NexusBadge = "NEXUS · OFFLINE";
        NexusTone = Tone.Warn;
    }
}
