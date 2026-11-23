using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Afterlife.App.ViewModels;

/// <summary>
/// While any mod is checked, the row of small tools in the APPLY bar gives way to the bulk actions; the status and
/// APPLY stay. The row says how many mods are checked and how many of them the list does not show (search, filters,
/// folded sections) and why; Esc clears the selection.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Checked mods the list does not show.</summary>
    [ObservableProperty] private int _hiddenCheckedCount;

    /// <summary>" (2 not visible)", or "" when every checked mod is in view.</summary>
    [ObservableProperty] private string _hiddenCheckedNote = "";

    /// <summary>Why some checked mods are not visible; null (no tooltip) when all are.</summary>
    [ObservableProperty] private string? _hiddenCheckedTip;

    /// <summary>Called from UpdateChecked; a fold or unfold rebuilds the chapters without touching the check marks.</summary>
    private void UpdateHiddenChecked()
    {
        var hidden = CheckedRows.Hidden(Mods, r => r.IsChecked, Visible, InFoldedSection);
        HiddenCheckedCount = hidden.Total;
        HiddenCheckedNote = CheckedRows.Note(hidden);
        HiddenCheckedTip = CheckedRows.Tip(hidden, !string.IsNullOrWhiteSpace(SearchText), Filters.HasActive);
    }

    /// <summary>The row's chapter header is folded (a category also folds with MODS around it).</summary>
    private bool InFoldedSection(ModRowVm row) =>
        CollectionChapters.TryGetValue(row.Chapter, out var chapter) && chapter.IsCollapsed;

    partial void OnCollectionChaptersChanged(IReadOnlyDictionary<string, CollectionChapterVm> value)
    {
        if (!_building) UpdateHiddenChecked();
    }

    /// <summary>Esc after closing a dialog and clearing the search: uncheck everything so the small tools come back.</summary>
    public bool ClearSelectionByEscape()
    {
        if (Overlay is not null || !HasMultiSelection) return false;
        ClearSelection();
        return true;
    }
}
