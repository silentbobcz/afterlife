using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>

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
/// "Scan manual mods" — mods found in the game that AFTERLIFE did not install, grouped; rescan with the
/// NETDECK network scan, reversible removal (core mods too), restore.
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
            // reading the folders and rebuilding the list can hold the window for a moment, which froze the sweep
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
