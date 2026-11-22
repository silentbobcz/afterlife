using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>
/// Sorting the mod list for viewing. The rows are kept twice: in load order (what the profile and every
/// reordering use) and as shown. A sort only changes the second, so it can never reach the game.
/// </summary>
public sealed partial class MainViewModel
{
    private const string SortKey = "ui.sort";

    /// <summary>Rows in load order, whatever the list shows.</summary>
    private IReadOnlyList<ModRowVm> _loadOrdered = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewSorted), nameof(SortLabel), nameof(SortNote))]
    private ListSort _sortBy;

    /// <summary>The list shows another order than the game loads; reordering by drag is off meanwhile.</summary>
    public bool IsViewSorted => SortBy != ListSort.LoadOrder;
    public string SortLabel => "⇅ " + ModSort.Label(SortBy);
    public string SortNote => $"Sorted by {ModSort.Label(SortBy).ToLowerInvariant()} for viewing only. The load order is unchanged; dragging mods is off until you go back.";

    [RelayCommand] private void CycleSort() => SortBy = ModSort.Next(SortBy);
    [RelayCommand] private void ResetSort() => SortBy = ListSort.LoadOrder;

    /// <summary>Restores the remembered sort without writing it back.</summary>
    private void RestoreSort(string? stored)
    {
        if (!Enum.TryParse<ListSort>(stored, out var sort) || sort == SortBy) return;
        _restoringSort = true;
        try { SortBy = sort; }
        finally { _restoringSort = false; }
    }

    private bool _restoringSort;

    /// <summary>Shows the same rows in the new order. Nothing is read or written besides the remembered choice.</summary>
    partial void OnSortByChanged(ListSort value)
    {
        if (_restoringSort) return;
        _ = _host.RunAsync(c => c.Store.SetMeta(SortKey, value.ToString()));
        if (_loadOrdered.Count == 0) return;
        var keep = SelectedMod;
        ShowRows(_loadOrdered);
        if (keep is not null && Mods.Contains(keep)) SelectedMod = keep;
        UpdateChecked();
    }

    /// <summary>Puts the rows (given in load order) into the list in the chosen order.</summary>
    private void ShowRows(IReadOnlyList<ModRowVm> loadOrdered)
    {
        _loadOrdered = loadOrdered;
        NumberRows(loadOrdered);
        var locked = IsViewSorted;
        foreach (var r in loadOrdered) r.SortLocked = locked;
        var shown = ModSort.View(loadOrdered, SortBy, r => r.Overview, r => r.BlockRank, r => r.IsCore);
        if (shown.SequenceEqual(Mods)) ModsView.Refresh(); // same rows in the same order: regroup only
        else Mods.Reset(shown);
        UpdateGroupNames();
    }
}
