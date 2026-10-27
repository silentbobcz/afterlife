using System.Windows.Data;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>
/// P95/P97: blocks of the mod list and their load order. CORE MODS stay on top; collections and MODS
/// can be moved; MODS holds the mods without category first and then the user's categories.
/// The list order is the load order: every change here reorders the selected profile.
/// </summary>
public sealed partial class MainViewModel
{
    public const string CategoryChapterPrefix = "CATEGORY · ";
    private const string LayoutKey = "ui.layout";
    private BlockLayout _layout = BlockLayout.Empty;

    private IReadOnlyList<string> CollectionSlugs => _collections.Select(c => c.Slug).ToList();

    private async Task LoadLayoutAsync()
    {
        try
        {
            (_collections, var layout, var folded) = await _host.RunAsync(c =>
                (c.Collections(), c.Store.GetMeta(LayoutKey), _collapsed is null ? c.Store.GetMeta("ui.collapsed") : null));
            _layout = BlockLayout.FromJson(layout);
            if (_collapsed is null) _collapsed = (folded ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex) when (IsExpected(ex)) { _collections = []; }
    }

    private Task SaveLayoutAsync(BlockLayout layout)
    {
        _layout = layout;
        var json = layout.ToJson();
        return _host.RunAsync(c => c.Store.SetMeta(LayoutKey, json));
    }

    /// <summary>Section of a mod: core, its first collection, its category or MODS.</summary>
    private string SectionOf(ModOverview m, BlockLayout layout, IReadOnlyDictionary<string, string> slugOfName)
    {
        if (m.IsCore) return "core";
        foreach (var name in m.Collections)
            if (slugOfName.TryGetValue(name, out var slug)) return BlockLayout.CollectionKey(slug);
        return layout.CategoryIdOf(m.Package.PackageId) is { } cat ? BlockLayout.CategoryKey(cat) : BlockLayout.ModsBlock;
    }

    private Dictionary<string, string> SlugOfName() =>
        _collections.GroupBy(c => c.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Slug, StringComparer.Ordinal);

    private string ChapterOfSection(string section)
    {
        if (section == "core") return CoreChapter;
        if (section == BlockLayout.ModsBlock) return PlainChapter;
        if (section.StartsWith(BlockLayout.CategoryPrefix, StringComparison.Ordinal)) return CategoryChapterPrefix + section[BlockLayout.CategoryPrefix.Length..];
        var slug = section[BlockLayout.CollectionPrefix.Length..];
        return CollectionChapterPrefix + (_collections.FirstOrDefault(c => c.Slug == slug)?.Name ?? slug);
    }

    /// <summary>Chapter, section and rank of every row; a collection without a record falls back to MODS (P94).</summary>
    private void AssignChapters(IEnumerable<ModRowVm> rows)
    {
        var slugOfName = SlugOfName();
        var sections = _layout.Sections(CollectionSlugs);
        var rank = sections.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i, StringComparer.Ordinal);
        foreach (var r in rows)
        {
            r.Section = SectionOf(r.Overview, _layout, slugOfName);
            r.Chapter = ChapterOfSection(r.Section);
            r.BlockRank = r.Section == "core" ? -1 : rank.TryGetValue(r.Section, out var i) ? i : sections.Count;
        }
    }

    /// <summary>
    /// P94/P95: the groups are declared up front, so MODS and empty categories keep their header
    /// and the groups appear in the load order. A search shows only the groups with matches (P83).
    /// </summary>
    private void UpdateGroupNames()
    {
        if (ModsView.GroupDescriptions.FirstOrDefault() is not PropertyGroupDescription g) return;
        var want = new List<string>();
        if (_overview is not null && Mods.Count > 0 && string.IsNullOrWhiteSpace(SearchText))
        {
            if (Mods.Any(m => m.IsCore)) want.Add(CoreChapter);
            want.AddRange(_layout.Sections(CollectionSlugs).Select(ChapterOfSection));
        }
        if (g.GroupNames.Cast<object>().Select(o => o as string).SequenceEqual(want)) return;
        g.GroupNames.Clear();
        foreach (var w in want) g.GroupNames.Add(w);
        ModsView.Refresh();
    }

    // ---------------- order ----------------
    private List<string> CurrentOrder() =>
        Mods.Where(m => !m.IsCore && m.Overview.Package.LayerOfModId is null).Select(m => m.PackageId).ToList();

    /// <summary>Saves the layout and writes the load order that follows it into the selected profile.</summary>
    private async Task ApplyLayoutAsync(BlockLayout layout, List<string> order, string message)
    {
        if (_overview is null) return;
        var before = Winners();
        var slugOfName = SlugOfName();
        var byId = Mods.ToDictionary(m => m.PackageId, m => m.Overview);
        var composed = BlockLayout.Compose(order, id => byId.TryGetValue(id, out var m) ? SectionOf(m, layout, slugOfName) : BlockLayout.ModsBlock,
            layout.Sections(CollectionSlugs));
        await SaveLayoutAsync(layout);
        var changed = !composed.SequenceEqual(CurrentOrder()) || !_overview.OrderIsCustom;
        if (changed) await _host.RunAsync(c => c.Reorder(_overview.Status.Selected.Id, composed));
        await RefreshAsync();
        Say(message + (changed ? " Load order follows the list: higher loads first." + FlipText(before) + " Apply to update the game." : ""), Tone.Info);
    }

    /// <summary>File conflicts and their winner, to tell what a move changed.</summary>
    private Dictionary<string, string?> Winners()
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (_overview is null) return result;
        foreach (var m in _overview.Mods)
            foreach (var c in m.Conflicts)
                result[c.Path] = c.WinnerName;
        return result;
    }

    private string FlipText(Dictionary<string, string?> before)
    {
        var after = Winners();
        var flips = before.Where(kv => after.TryGetValue(kv.Key, out var w) && w != kv.Value && w is not null)
            .GroupBy(kv => (Old: kv.Value, New: after[kv.Key]))
            .Select(g => (g.Key.Old, g.Key.New, Count: g.Count())).ToList();
        if (flips.Count == 0) return "";
        var first = flips[0];
        var total = flips.Sum(f => f.Count);
        return $" {total} conflicting file(s) now come from another mod" +
               (first.Old is null ? "" : $" (for example {first.New} now wins over {first.Old})") + ".";
    }

    /// <summary>P97: load position of a block or category (for drag and drop between blocks).</summary>
    public int BlockRankOf(string section)
    {
        var sections = _layout.Sections(CollectionSlugs);
        var i = sections.ToList().IndexOf(section);
        return i < 0 ? int.MaxValue : i;
    }

    private static bool InModsFamily(string chapter) =>
        chapter == PlainChapter || chapter.StartsWith(CategoryChapterPrefix, StringComparison.Ordinal);

    private static string? CategoryIdOfChapter(string chapter) =>
        chapter.StartsWith(CategoryChapterPrefix, StringComparison.Ordinal) ? chapter[CategoryChapterPrefix.Length..] : null;

    /// <summary>
    /// P95: mods of MODS and its categories move between them; collection mods only inside their own collection;
    /// core mods never. Returns null when the drop is fine, otherwise why not.
    /// </summary>
    public string? WhyNotDrop(IReadOnlyList<string> moved, string targetChapter)
    {
        if (IsModsOff || targetChapter == CoreChapter) return "CORE MODS keep their place";
        var rows = Mods.Where(m => moved.Contains(m.PackageId)).ToList();
        if (rows.Count == 0) return "nothing to move";
        if (InModsFamily(targetChapter))
            return rows.All(r => r.InModsBlock) ? null : "mods of a collection stay in their collection";
        return rows.All(r => r.Chapter == targetChapter) ? null : "a collection holds only its own mods";
    }

    /// <summary>Drag and drop onto a row: before or after it; into its category (P95).</summary>
    public Task MoveModsAsync(IReadOnlyList<string> moved, string targetId, bool after) => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff) return;
        var target = Mods.FirstOrDefault(m => m.PackageId == targetId);
        if (target is null) return;
        if (WhyNotDrop(moved, target.Chapter) is { } why) { Say("Not moved: " + why + ".", Tone.Warn); return; }
        var order = CurrentOrder();
        var movedSet = moved.Where(order.Contains).ToList();
        if (movedSet.Count == 0 || movedSet.Contains(targetId)) return;
        order.RemoveAll(movedSet.Contains);
        var i = order.IndexOf(targetId);
        if (i < 0) i = order.Count; else if (after) i++;
        order.InsertRange(i, movedSet);
        var layout = _layout;
        var into = "";
        if (InModsFamily(target.Chapter))
        {
            var cat = CategoryIdOfChapter(target.Chapter);
            var changes = Mods.Where(m => movedSet.Contains(m.PackageId) && CategoryIdOfChapter(m.Chapter) != cat).ToList();
            layout = layout.Assign(movedSet, cat);
            if (changes.Count > 0) into = $"{changes.Count} mod(s) moved to {(cat is null ? "MODS" : CategoryName(cat))}.";
        }
        await ApplyLayoutAsync(layout, order, into.Length > 0 ? into : "Load order changed.");
        if (into.Length > 0) SetAllChecked(false); // P100: moved into a category, selection done
    });

    /// <summary>P95: drop onto a header (or MOVE TO CATEGORY): the mods go to the end of that category / MODS.</summary>
    public Task MoveModsToChapterAsync(IReadOnlyList<string> moved, string chapter) => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff || !InModsFamily(chapter)) return;
        if (WhyNotDrop(moved, chapter) is { } why) { Say("Not moved: " + why + ".", Tone.Warn); return; }
        var cat = CategoryIdOfChapter(chapter);
        var order = CurrentOrder();
        var movedSet = moved.Where(order.Contains).ToList();
        if (movedSet.Count == 0) return;
        order.RemoveAll(movedSet.Contains);
        order.AddRange(movedSet); // the stable sort by section puts them at the end of their section
        await ApplyLayoutAsync(_layout.Assign(movedSet, cat), order,
            $"{movedSet.Count} mod(s) moved to {(cat is null ? "MODS" : CategoryName(cat))}.");
        SetAllChecked(false); // P100
    });

    public bool CanDropOnChapter(IReadOnlyList<string> moved, string chapter) => InModsFamily(chapter) && WhyNotDrop(moved, chapter) is null;

    private string CategoryName(string id) => _layout.Categories.FirstOrDefault(c => c.Id == id)?.Name ?? "category";

    // ---------------- blocks (P97) ----------------
    /// <summary>Moves a block (collection, MODS) or a category before or after another one.</summary>
    public Task MoveBlockAsync(string block, string target, bool after) => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff || block == target) return;
        var layout = _layout;
        string name;
        if (block.StartsWith(BlockLayout.CategoryPrefix, StringComparison.Ordinal))
        {
            var ids = layout.Categories.Select(c => BlockLayout.CategoryKey(c.Id)).ToList();
            if (target == BlockLayout.ModsBlock) { target = ids.FirstOrDefault() ?? block; after = false; }
            if (!ids.Contains(target)) { Say("Not moved: categories stay inside MODS.", Tone.Warn); return; }
            var moved = BlockLayout.MoveBefore(ids, block, target, after);
            var byKey = layout.Categories.ToDictionary(c => BlockLayout.CategoryKey(c.Id));
            layout = layout.WithCategories(moved.Select(k => byKey[k]).ToList());
            name = "Category " + byKey[block].Name;
        }
        else
        {
            if (target.StartsWith(BlockLayout.CategoryPrefix, StringComparison.Ordinal)) target = BlockLayout.ModsBlock;
            var blocks = layout.NormalizedBlocks(CollectionSlugs);
            if (!blocks.Contains(block) || !blocks.Contains(target)) return;
            layout = layout.WithBlocks(BlockLayout.MoveBefore(blocks, block, target, after));
            name = block == BlockLayout.ModsBlock ? "MODS" : "Collection " + (_collections.FirstOrDefault(c => BlockLayout.CollectionKey(c.Slug) == block)?.Name ?? "");
        }
        await ApplyLayoutAsync(layout, CurrentOrder(), name + " moved.");
    });

    /// <summary>Arrow buttons: one step up or down among its siblings.</summary>
    private Task StepBlockAsync(string block, int delta)
    {
        var siblings = block.StartsWith(BlockLayout.CategoryPrefix, StringComparison.Ordinal)
            ? _layout.Categories.Select(c => BlockLayout.CategoryKey(c.Id)).ToList()
            : _layout.NormalizedBlocks(CollectionSlugs).ToList();
        var i = siblings.IndexOf(block);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= siblings.Count) return Task.CompletedTask;
        return MoveBlockAsync(block, siblings[j], delta > 0);
    }

    private (IRelayCommand Up, IRelayCommand Down) StepCommands(string block)
    {
        var siblings = block.StartsWith(BlockLayout.CategoryPrefix, StringComparison.Ordinal)
            ? _layout.Categories.Select(c => BlockLayout.CategoryKey(c.Id)).ToList()
            : _layout.NormalizedBlocks(CollectionSlugs).ToList();
        var i = siblings.IndexOf(block);
        var canEdit = !IsModsOff && _overview?.Status.Unfinished is null;
        return (new AsyncRelayCommand(() => StepBlockAsync(block, -1), () => canEdit && i > 0),
                new AsyncRelayCommand(() => StepBlockAsync(block, 1), () => canEdit && i >= 0 && i < siblings.Count - 1));
    }

    // ---------------- categories (P95) ----------------
    private async Task<string?> AskCategoryNameAsync(string title, string initial, string? exceptId)
    {
        while (true)
        {
            var name = await AskAsync(new TextPromptVm(title, "Category name (for example UI, Weapons, Clothing)", initial)
            { MaxLength = BlockLayout.MaxNameLength, ConfirmLabel = exceptId is null ? "CREATE" : "RENAME" });
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (!_layout.Categories.Any(c => c.Id != exceptId && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))) return name;
            Say($"A category {name} already exists; choose another name.", Tone.Warn);
            initial = name;
        }
    }

    [RelayCommand]
    private Task AddCategory() => RunUiAsync(async () =>
    {
        if (_overview is null) return;
        var name = await AskCategoryNameAsync("NEW CATEGORY", "", null);
        if (name is null) return;
        await SaveLayoutAsync(_layout.WithCategories([.. _layout.Categories, new ModCategory(BlockLayout.NewCategoryId(), name)]));
        await RefreshAsync();
        Say($"Category {name} added at the end of MODS. Drag mods onto it, or select them and use MOVE TO CATEGORY.", Tone.Ok);
    });

    private Task RenameCategoryAsync(string id) => RunUiAsync(async () =>
    {
        var current = _layout.Categories.FirstOrDefault(c => c.Id == id);
        if (current is null) return;
        var name = await AskCategoryNameAsync("RENAME CATEGORY", current.Name, id);
        if (name is null || name == current.Name) return;
        await SaveLayoutAsync(_layout.WithCategories(_layout.Categories.Select(c => c.Id == id ? c with { Name = name } : c).ToList()));
        await RefreshAsync();
    });

    private Task RemoveCategoryAsync(string id) => RunUiAsync(async () =>
    {
        var current = _layout.Categories.FirstOrDefault(c => c.Id == id);
        if (current is null) return;
        var members = Mods.Count(m => m.Section == BlockLayout.CategoryKey(id));
        if (members > 0 && !await AskAsync(new ConfirmVm("REMOVE CATEGORY?",
                $"Category {current.Name} is removed; its {members} mod(s) stay in the library and in the profile and go back to MODS without a category. Their load order moves with them.",
                "REMOVE"))) return;
        var layout = _layout.WithCategories(_layout.Categories.Where(c => c.Id != id).ToList());
        if (members == 0) { await SaveLayoutAsync(layout); await RefreshAsync(); Say($"Category {current.Name} removed.", Tone.Info); return; }
        await ApplyLayoutAsync(layout, CurrentOrder(), $"Category {current.Name} removed; its mods are back in MODS.");
    });

    /// <summary>P95: bulk action for the checked mods: choose the category.</summary>
    [RelayCommand]
    private Task BulkMoveToCategory() => RunUiAsync(async () =>
    {
        var ids = _multi.Select(m => m.PackageId).ToList();
        if (ids.Count == 0 || _overview is null) return;
        if (_multi.Any(m => !m.InModsBlock))
        {
            Say("Only mods outside core and collections can go to a category; uncheck the others first.", Tone.Warn);
            return;
        }
        var options = new List<string> { "MODS (no category)" };
        options.AddRange(_layout.Categories.Select(c => c.Name));
        options.Add("+ New category…");
        var vm = new ChoiceVm("MOVE TO CATEGORY",
            $"{ids.Count} selected mod(s) go to the end of the chosen category. Categories load in their order inside MODS, so the load order of these mods changes.",
            options, "MOVE", selected: options.Count > 2 ? 1 : options.Count - 1);
        var pick = await AskAsync(vm, v => ((ChoiceVm)v).Result);
        if (pick is null) return;
        string chapter;
        if (pick == 0) chapter = PlainChapter;
        else if (pick == options.Count - 1)
        {
            var name = await AskCategoryNameAsync("NEW CATEGORY", "", null);
            if (name is null) return;
            var cat = new ModCategory(BlockLayout.NewCategoryId(), name);
            await SaveLayoutAsync(_layout.WithCategories([.. _layout.Categories, cat]));
            chapter = CategoryChapterPrefix + cat.Id;
        }
        else chapter = CategoryChapterPrefix + _layout.Categories[pick.Value - 1].Id;
        await MoveModsToChapterAsync(ids, chapter);
    });

    /// <summary>Forgets categories of mods deleted from the library (keeps the stored layout small).</summary>
    private void PruneLayout()
    {
        if (_overview is null || _layout.CategoryOf.Count == 0) return;
        var ids = _overview.Mods.Select(m => m.Package.PackageId).ToHashSet(StringComparer.Ordinal);
        var pruned = _layout.Prune(ids);
        if (!ReferenceEquals(pruned, _layout)) _ = SaveLayoutAsync(pruned);
    }
}
