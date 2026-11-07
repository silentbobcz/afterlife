using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>

/// <summary>What a collection contains compared with the library, before it is added.</summary>
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

/// <summary>One collection in the library: progress, update, order, instructions, remove.</summary>
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
    /// <summary>The running (or paused) download of this collection, if any.</summary>
    public CollectionProgressVm? Progress { get; init; }
    public bool HasProgress => Progress is not null;
    public bool ShowContinue => HasMissing && Progress is null;
    public override string Title => "COLLECTION · " + Record.Name.ToUpperInvariant();
    private readonly string _headerBase, _headerTail, _inLibrary;
    /// <summary>While downloading, the count is in the progress block (it stays current there).</summary>
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

/// <summary>Download of a collection, shown in its window: overall bar, current file bar, items, pause and resume.</summary>
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
    /// <summary>The file in progress as one line: name, and the percent when AFTERLIFE downloads it itself (Premium).</summary>
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
