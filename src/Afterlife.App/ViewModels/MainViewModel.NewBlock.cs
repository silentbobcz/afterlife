using System.Windows.Input;
using Afterlife.Core;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>
/// NEW: a block for the mods the user just imported or downloaded (<see cref="BlockLayout.NewIds"/>). It loads where
/// it stands among the blocks like any other block, first by default. Its mods leave it when dragged into MODS or a
/// category, or with MOVE ALL TO MODS; nothing is ever moved into it. An empty NEW is not shown but keeps its place.
/// </summary>
public sealed partial class MainViewModel
{
    public const string NewChapter = "NEW";

    /// <summary>NEW has rows to show; an empty NEW has no header, no arrows and no place among the arrow steps.</summary>
    private bool NewHasMods => Mods.Any(m => m.Section == BlockLayout.NewBlock);

    /// <summary>What the arrows of a block step over: categories inside MODS, otherwise the blocks shown in the list.</summary>
    private List<string> SiblingsOf(string block) => block.StartsWith(BlockLayout.CategoryPrefix, StringComparison.Ordinal)
        ? _layout.Categories.Select(c => BlockLayout.CategoryKey(c.Id)).ToList()
        : _layout.NormalizedBlocks(CollectionSlugs).Where(b => b != BlockLayout.NewBlock || NewHasMods).ToList();

    /// <summary>Puts mods the user just added into NEW. Saved before the list is rebuilt, so the profile is written once.</summary>
    private async Task PutIntoNewAsync(IReadOnlyList<string> packageIds)
    {
        var layout = _layout.AddNew(packageIds);
        if (!ReferenceEquals(layout, _layout)) await SaveLayoutAsync(layout);
    }

    /// <summary>The header of NEW, only while it has mods: no A→Z, no rename or remove; arrows and MOVE ALL TO MODS.</summary>
    private void AddNewChapter(Dictionary<string, CollectionChapterVm> map, IReadOnlyList<ModRowVm> rows, bool canEdit,
        Func<CollectionChapterVm, Func<ModRowVm, bool>, CollectionChapterVm> marks, Func<string, bool> folded, Func<string, ICommand> fold)
    {
        if (_overview is null) return;
        var ids = rows.Where(r => r.InNewBlock && r.Overview.Package.LayerOfModId is null).Select(r => r.PackageId).ToList();
        if (ids.Count == 0) return;
        var selected = _overview.Status.Selected;
        var on = ids.Count(selected.IsEnabled);
        var (up, down) = StepCommands(BlockLayout.NewBlock);
        map[NewChapter] = marks(new CollectionChapterVm(ChapterKind.New, BlockLayout.NewBlock, "NEW", "Freshly installed · drag each mod to where it belongs",
            on, ids.Count, canEdit, folded(NewChapter), new AsyncRelayCommand(() => SetChapterEnabledAsync(NewChapter, on == 0)), null, fold(NewChapter))
        {
            BlockKey = BlockLayout.NewBlock, MoveUpCommand = up, MoveDownCommand = down,
            MoveAllCommand = canEdit ? new AsyncRelayCommand(MoveAllNewToModsAsync) : null,
        }, r => r.InNewBlock);
    }

    /// <summary>Empties NEW: its mods go to the end of MODS without a category, in their current order (one write).</summary>
    private Task MoveAllNewToModsAsync() => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff) return;
        var fresh = Mods.Where(m => m.Section == BlockLayout.NewBlock).Select(m => m.PackageId).ToHashSet(StringComparer.Ordinal);
        var order = CurrentOrder();
        var moved = order.Where(fresh.Contains).ToList();
        if (moved.Count == 0) return;
        order.RemoveAll(fresh.Contains);
        order.AddRange(moved); // the stable sort by section puts them at the end of MODS without a category
        await ApplyLayoutAsync(_layout.RemoveNew(_layout.NewIds.ToList()), order, $"{Plural.Of(moved.Count, "mod")} moved from NEW to the end of MODS.");
    });
}
