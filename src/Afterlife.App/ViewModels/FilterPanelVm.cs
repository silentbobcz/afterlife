using System.Collections.ObjectModel;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

public enum FilterPanelMode { Open, Rail, Hidden }

/// <summary>One line of the filter panel: off, must match or must not match.</summary>
public sealed partial class FilterItemVm(string key, string label, Tone tone, string? glyph, Action changed) : ObservableObject
{
    public string Key { get; } = key;
    public string Label { get; } = label;
    public Tone Tone { get; } = tone;
    /// <summary>Mark instead of the colour square (▲ ⚠ ✕ for what needs attention).</summary>
    public string? Glyph { get; } = glyph;
    public bool HasGlyph => Glyph is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIncluded), nameof(IsExcluded))]
    private FilterState _state;

    [ObservableProperty] private int _count;

    public bool IsIncluded => State == FilterState.Include;
    public bool IsExcluded => State == FilterState.Exclude;

    [RelayCommand]
    private void Toggle()
    {
        State = ModFilter.Next(State);
        changed();
    }
}

public sealed record FilterGroupVm(string Title, ObservableCollection<FilterItemVm> Items);

/// <summary>
/// Filter panel next to the mod list. Open, collapsed to a strip, or hidden; the filters themselves are a second
/// condition beside the search and never change the load order.
/// </summary>
public sealed partial class FilterPanelVm : ObservableObject
{
    private readonly FilterGroupVm _tags;

    public FilterPanelVm()
    {
        FilterItemVm Item(string key, string label, Tone tone, string? glyph = null) => new(key, label, tone, glyph, OnFiltersChanged);
        Groups =
        [
            new("STATE", [Item(ModFilter.On, "Switched on", Tone.Brand), Item(ModFilter.Off, "Switched off", Tone.Neutral), Item(ModFilter.Pending, "Not applied yet", Tone.Info)]),
            new("NEEDS ATTENTION", [Item(ModFilter.Update, "Update available", Tone.Info, "▲"), Item(ModFilter.Conflict, "Conflict", Tone.Warn, "⚠"), Item(ModFilter.Missing, "Missing requirement", Tone.Block, "✕")]),
            new("BLOCK", [Item(ModFilter.Core, "Core mods", Tone.Brand), Item(ModFilter.Collection, "Collections", Tone.Info), Item(ModFilter.Mods, "My mods", Tone.Plain)]),
        ];
        _tags = new FilterGroupVm("TAGS", []);
        Groups.Add(_tags);
        QuickItems = Groups[1].Items;
    }

    public ObservableCollection<FilterGroupVm> Groups { get; }
    /// <summary>The three NEEDS ATTENTION filters, also on the collapsed strip.</summary>
    public IReadOnlyList<FilterItemVm> QuickItems { get; }

    private IEnumerable<FilterItemVm> All => Groups.SelectMany(g => g.Items);

    /// <summary>Filters changed: the list has to be filtered again.</summary>
    public event Action? Changed;
    /// <summary>The user opened, collapsed or hid the panel (remembered in the library).</summary>
    public event Action<FilterPanelMode>? ModeChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(IsRail), nameof(IsShown), nameof(ButtonOn))]
    private FilterPanelMode _mode = FilterPanelMode.Open;

    /// <summary>Window too narrow for the open panel: it shows as the strip until there is room again.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(IsRail))]
    private bool _isNarrow;

    [ObservableProperty] private bool _matchAll = true;

    public bool IsShown => Mode != FilterPanelMode.Hidden;
    public bool IsOpen => Mode == FilterPanelMode.Open && !IsNarrow;
    public bool IsRail => Mode == FilterPanelMode.Rail || (Mode == FilterPanelMode.Open && IsNarrow);

    public int ActiveCount => _active.Count;
    public bool HasActive => ActiveCount > 0;
    public bool ButtonOn => Mode != FilterPanelMode.Hidden || HasActive;
    public string ButtonLabel => HasActive ? $"⧩ FILTERS · {ActiveCount}" : "⧩ FILTERS";
    public string Summary => HasActive ? $"{ActiveCount} filter{(ActiveCount == 1 ? "" : "s")} on" : "No filter on";

    partial void OnMatchAllChanged(bool value) => OnFiltersChanged();

    /// <summary>Restores the remembered mode without writing it back.</summary>
    public void Restore(string? mode)
    {
        if (Enum.TryParse<FilterPanelMode>(mode, out var m)) Mode = m;
    }

    private void SetMode(FilterPanelMode m)
    {
        Mode = m;
        ModeChanged?.Invoke(m);
    }

    [RelayCommand] private void Open() => SetMode(FilterPanelMode.Open);
    [RelayCommand] private void Collapse() => SetMode(FilterPanelMode.Rail);
    [RelayCommand] private void Hide() => SetMode(FilterPanelMode.Hidden);

    /// <summary>The button next to the search: hidden → open → strip → hidden.</summary>
    [RelayCommand]
    private void Cycle() => SetMode(Mode switch
    {
        FilterPanelMode.Hidden => FilterPanelMode.Open,
        FilterPanelMode.Open => FilterPanelMode.Rail,
        _ => FilterPanelMode.Hidden,
    });

    [RelayCommand] private void SetMatchAll(string? all) => MatchAll = all != "any";

    [RelayCommand]
    private void ClearAll()
    {
        if (!HasActive) return;
        foreach (var i in All) i.State = FilterState.Off;
        OnFiltersChanged();
    }

    private Dictionary<string, FilterState> _active = new(StringComparer.OrdinalIgnoreCase);

    public bool Matches(ModRowVm row) => _active.Count == 0 || ModFilter.Matches(row.Facets, _active, MatchAll);

    /// <summary>Counts per filter and the tag list, once per refresh of the list. Chosen states survive.</summary>
    public void Recount(IReadOnlyCollection<ModRowVm> rows)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
            foreach (var f in r.Facets) counts[f] = counts.GetValueOrDefault(f) + 1;

        var tagKeys = counts.Keys.Where(k => k.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(k => counts[k]).ThenBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        var old = _tags.Items.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase);
        if (!tagKeys.SequenceEqual(_tags.Items.Select(i => i.Key), StringComparer.OrdinalIgnoreCase))
        {
            _tags.Items.Clear();
            foreach (var k in tagKeys)
                _tags.Items.Add(old.TryGetValue(k, out var keep) ? keep : new FilterItemVm(k, k[4..], Tone.Plain, null, OnFiltersChanged));
        }
        foreach (var i in All) i.Count = counts.GetValueOrDefault(i.Key);
        // a tag that is gone from the library cannot stay as an invisible filter
        if (_active.Keys.Any(k => !All.Any(i => string.Equals(i.Key, k, StringComparison.OrdinalIgnoreCase)))) OnFiltersChanged();
    }

    private void OnFiltersChanged()
    {
        _active = All.Where(i => i.State != FilterState.Off).ToDictionary(i => i.Key, i => i.State, StringComparer.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(HasActive));
        OnPropertyChanged(nameof(ButtonOn));
        OnPropertyChanged(nameof(ButtonLabel));
        OnPropertyChanged(nameof(Summary));
        Changed?.Invoke();
    }
}
