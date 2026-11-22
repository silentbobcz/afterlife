using System.Globalization;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>
/// The free load order inside MODS and its categories: rows in the profile's order, layers ("└ name") right below
/// their mod, the position number of every row, A→Z for one section and keeping the profile equal to the list.
/// A profile in the game's default order (no modlist.txt) is shown by name until a mod is dragged.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Profiles shown by name once more, as before the order became free (see <see cref="LoadOrder.PendingTakeOver"/>).</summary>
    private HashSet<string>? _takeOver;

    /// <summary>Layer package id → its mod's package id, for layers shown right below their mod.</summary>
    private Dictionary<string, string> _parentOf = new(StringComparer.Ordinal);

    private bool _syncingOrder;

    private static readonly StringComparer RowNames = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);

    private async Task LoadTakeOverAsync()
    {
        _takeOver = await _host.RunAsync(c =>
        {
            var stored = c.Store.GetMeta(LoadOrder.TakeOverKey);
            var pending = LoadOrder.PendingTakeOver(stored, c.Profiles.Select(p => p.Id));
            if (stored is null) c.Store.SetMeta(LoadOrder.TakeOverKey, LoadOrder.FormatTakeOver(pending));
            return pending;
        });
    }

    private async Task ForgetTakeOverAsync(string profileId)
    {
        if (_takeOver is null || !_takeOver.Remove(profileId)) return;
        var text = LoadOrder.FormatTakeOver(_takeOver);
        try { await _host.RunAsync(c => c.Store.SetMeta(LoadOrder.TakeOverKey, text)); }
        catch (Exception ex) when (IsExpected(ex)) { }
    }

    /// <summary>Every layer row with the row of its mod.</summary>
    private static IEnumerable<(ModRowVm Layer, ModRowVm Parent)> ParentRows(IEnumerable<ModRowVm> rows)
    {
        var list = rows.ToList();
        var mainOf = list.Where(r => r.Overview.Package.LayerOfModId is null)
            .GroupBy(r => r.Overview.Package.ModId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var r in list)
            if (r.Overview.Package.LayerOfModId is { } of && mainOf.TryGetValue(of, out var parent))
                yield return (r, parent);
    }

    /// <summary>
    /// Rows in load order. CORE MODS and collections keep the order they come in; MODS and categories follow the
    /// profile, or go by name while the profile has the game's default order (or is shown for the first time
    /// since the order became free, so an existing library looks exactly as before).
    /// </summary>
    private List<ModRowVm> ArrangeRows(List<ModRowVm> rows, InstallOverview o)
    {
        var byName = !o.OrderIsCustom || _takeOver?.Contains(o.Status.Selected.Id) == true;
        _parentOf = ParentRows(rows).Where(x => x.Layer.Section == x.Parent.Section)
            .ToDictionary(x => x.Layer.PackageId, x => x.Parent.PackageId, StringComparer.Ordinal);
        var parentOf = _parentOf;
        if (!byName)
        {
            // the profile's entries decide, also for a mod that provides a framework (the overview lists those first);
            // mods not in the profile yet keep the end, a layer the place of its mod
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var e in o.Status.Selected.Entries) index.TryAdd(e.PackageId, index.Count);
            int IndexOf(ModRowVm r) =>
                index.TryGetValue(parentOf.TryGetValue(r.PackageId, out var parent) ? parent : r.PackageId, out var at) ? at : int.MaxValue;
            rows = rows.Select((r, n) => (Row: r, At: n)).OrderBy(x => IndexOf(x.Row)).ThenBy(x => x.At).Select(x => x.Row).ToList();
        }
        return LoadOrder.Arrange(rows, r => r.PackageId, r => r.BlockRank,
            r => parentOf.TryGetValue(r.PackageId, out var p) ? p : null,
            r => byName && r.InModsBlock, r => r.Overview.Package.ModName, r => r.Overview.Package.Version, RowNames);
    }

    /// <summary>Position numbers after CORE MODS, counted like <see cref="CurrentOrder"/> (and so like modlist.txt).</summary>
    private void NumberRows(IReadOnlyList<ModRowVm> loadOrdered)
    {
        var tip = _overview?.OrderIsCustom == true
            ? "Position in the load order"
            : "Position in the load order. Until you change the order, the game loads mod archives by file name.";
        var n = 0;
        foreach (var r in loadOrdered)
        {
            var counted = !IsModsOff && !r.IsCore && r.Overview.Package.LayerOfModId is null;
            r.SetOrder(counted ? $"#{++n}" : null, tip);
        }
    }

    /// <summary>Where a dragged row lands: <see cref="Target"/> before or after, with the drop line drawn on <see cref="Hint"/>.</summary>
    public sealed record DropSpot(ModRowVm Target, bool After, ModRowVm Hint);

    /// <summary>
    /// The place for a drop on a row. A layer is part of its mod, so a drop on it lands right after that mod and its
    /// layers; null where nothing can be dropped (core mods, a layer shown apart from its mod).
    /// </summary>
    public DropSpot? DropTargetOf(ModRowVm row, bool after)
    {
        if (row.IsCore) return null;
        var target = row;
        if (row.Overview.Package.LayerOfModId is not null)
        {
            if (!_parentOf.TryGetValue(row.PackageId, out var parentId)) return null;
            var parent = _loadOrdered.FirstOrDefault(r => r.PackageId == parentId);
            if (parent is null) return null;
            target = parent;
            after = true;
        }
        var hint = after ? _loadOrdered.LastOrDefault(r => _parentOf.TryGetValue(r.PackageId, out var p) && p == target.PackageId) ?? target : target;
        return new DropSpot(target, after, hint);
    }

    /// <summary>
    /// With a custom load order (modlist.txt) the profile follows the list: mods new in the library join it where the
    /// list shows them (the end of MODS), a mod moved between categories in another profile follows its category,
    /// and the archive order is refreshed when a mod's archives changed (an update). APPLY puts it in the game.
    /// </summary>
    private async Task SyncSortedOrderAsync(InstallOverview o)
    {
        if (_syncingOrder || IsModsOff || o.Status.Unfinished is not null) return;
        var profile = o.Status.Selected;
        if (!o.OrderIsCustom) { await ForgetTakeOverAsync(profile.Id); return; }
        var slugOfName = SlugOfName();
        var byId = _loadOrdered.ToDictionary(m => m.PackageId, m => m.Overview, StringComparer.Ordinal);
        var composed = BlockLayout.Compose(CurrentOrder(), id => byId.TryGetValue(id, out var m) ? SectionOf(m, _layout, slugOfName) : BlockLayout.ModsBlock,
            _layout.Sections(CollectionSlugs));
        var set = composed.ToHashSet(StringComparer.Ordinal);
        var now = profile.Entries.Select(e => e.PackageId).Where(set.Contains).ToList();
        var packages = byId.Values.Select(m => m.Package).ToDictionary(p => p.PackageId, StringComparer.Ordinal);
        var archives = ProfileService.ArchiveOrderOf(composed, packages);
        if (now.SequenceEqual(composed) && archives.SequenceEqual(profile.ArchiveOrder.Order, StringComparer.OrdinalIgnoreCase))
        {
            await ForgetTakeOverAsync(profile.Id);
            return;
        }
        _syncingOrder = true;
        try
        {
            await _host.RunAsync(c => c.Reorder(profile.Id, composed));
            await ForgetTakeOverAsync(profile.Id);
            await RefreshAsync();
        }
        catch (Exception ex) when (IsExpected(ex)) { }
        finally { _syncingOrder = false; }
    }

    /// <summary>A→Z in the header of MODS or a category; null while the profile cannot be edited.</summary>
    private IRelayCommand? SortCommandOf(string section, bool canEdit) =>
        canEdit ? new AsyncRelayCommand(() => SortSectionByNameAsync(section)) : null;

    /// <summary>Sorts one section (MODS without category, or one category) by name once and writes it to the profile.</summary>
    private Task SortSectionByNameAsync(string section) => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff) return;
        var rows = _loadOrdered.ToDictionary(r => r.PackageId, StringComparer.Ordinal);
        var order = CurrentOrder();
        var sorted = LoadOrder.SortSection(order, id => rows.TryGetValue(id, out var r) ? r.Section : "", section,
            id => rows[id].Overview.Package.ModName, id => rows[id].Overview.Package.Version, RowNames);
        var isMods = section == BlockLayout.ModsBlock;
        var name = isMods ? "" : CategoryName(section[BlockLayout.CategoryPrefix.Length..]);
        var label = isMods ? "MODS" : "the category " + name;
        var title = isMods ? "MODS" : "Category " + name;
        if (sorted.SequenceEqual(order)) { Say(title + " is already sorted by name.", Tone.Info); return; }
        if (!await AskAsync(new ConfirmVm("SORT BY NAME?",
                $"Sort {label} by name? The order you set by hand in this section is replaced. Nothing changes in the game before APPLY CHANGES.",
                "SORT"))) return;
        await ApplyLayoutAsync(_layout, sorted, title + " sorted by name.");
    });
}
