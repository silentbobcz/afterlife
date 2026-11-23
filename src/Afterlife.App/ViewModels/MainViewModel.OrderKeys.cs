using Afterlife.Core;

namespace Afterlife.App.ViewModels;

/// <summary>
/// Moving mods in the load order without the mouse: Ctrl+Up / Down by one row, Ctrl+Home / End to the start or
/// end of the section, and LOAD BEFORE in the Conflicts tab of the details.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The mods a key moves: the checked ones, else the selected row; core mods, layers and locked rows never.</summary>
    private List<ModRowVm> KeyMovable()
    {
        var rows = Mods.Where(m => m.IsChecked).ToList();
        if (rows.Count == 0 && SelectedMod is { } one) rows.Add(one);
        return rows.Where(r => r.CanDrag).ToList();
    }

    /// <summary>The keys apply in the load order view only, to rows that can be dragged, with no job running.</summary>
    public bool CanMoveByKey =>
        _overview is not null && !IsModsOff && !IsViewSorted && !IsBusy && !IsOperating && KeyMovable().Count > 0;

    /// <summary>Moves the checked mods (or the selected row) inside their section; a move that changes nothing is silent.</summary>
    public Task MoveSelectionAsync(MoveStep step) => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff || IsViewSorted) return;
        var rows = KeyMovable();
        if (rows.Count == 0) return;
        var sectionOf = _loadOrdered.ToDictionary(r => r.PackageId, r => r.Section, StringComparer.Ordinal);
        var current = CurrentOrder();
        var order = LoadOrder.MoveInSection(current, rows.Select(r => r.PackageId), id => sectionOf.TryGetValue(id, out var s) ? s : "", step);
        if (order.SequenceEqual(current)) return;
        await ApplyLayoutAsync(_layout, order, "Load order changed.");
    });

    /// <summary>
    /// The row of the mod that now wins <paramref name="winnerName"/>'s file, when a mod can be moved before it:
    /// both mods in MODS or its categories (not layers), or the mod in NEW and the other one in NEW, MODS or a
    /// category (a mod never moves into NEW); the load order not locked. Null otherwise.
    /// </summary>
    public ModRowVm? LoadBeforeTarget(string modId, string? winnerName)
    {
        if (winnerName is null || IsModsOff) return null;
        var self = Mods.FirstOrDefault(m => m.PackageId == modId);
        if (self is null || !self.CanToggle || self.IsCore || !self.InFreeBlock || self.Overview.Package.LayerOfModId is not null) return null;
        return Mods.FirstOrDefault(m => m.PackageId != modId && !m.IsCore && (m.InModsBlock || (m.InNewBlock && self.InNewBlock)) && m.Overview.Package.LayerOfModId is null
            && string.Equals(m.Overview.Package.ModName, winnerName, StringComparison.Ordinal));
    }

    /// <summary>Moves a mod to just before another one, into its category if that is another one (like a drop on its row).</summary>
    public Task LoadBeforeAsync(string modId, string otherId) => MoveModsAsync([modId], otherId, after: false);
}
