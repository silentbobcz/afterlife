using Afterlife.Core;

namespace Afterlife.Tests;

/// <summary>The free load order inside MODS and its categories: moves, groups, layers, A→Z, new mods, old libraries.</summary>
public class FreeLoadOrderTests
{
    private const int Mods = 1, CatA = 2;
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

    /// <summary>A row of the list: section rank, name, the mod it is a layer of.</summary>
    private sealed record Row(string Id, int Rank, string Name, string? Parent = null, string Version = "1.0");

    private static List<string> Arrange(IReadOnlyList<Row> rows, bool byName) =>
        LoadOrder.Arrange(rows, r => r.Id, r => r.Rank, r => r.Parent, _ => byName, r => r.Name, r => r.Version, Names).Select(r => r.Id).ToList();

    private static readonly string[] Sections = ["mods", "category:a"];

    [Fact]
    public void A_mod_moves_anywhere_inside_its_section_and_the_section_keeps_its_place()
    {
        var sectionOf = new Dictionary<string, string> { ["m1"] = "mods", ["m2"] = "mods", ["m3"] = "mods", ["a1"] = "category:a" };
        var moved = LoadOrder.MoveGroup(["m1", "m2", "m3", "a1"], ["m3"], "m1", after: false);
        Assert.Equal(["m3", "m1", "m2", "a1"], moved);
        Assert.Equal(moved, BlockLayout.Compose(moved, id => sectionOf[id], Sections));
        Assert.Equal(["m2", "m1", "m3", "a1"], LoadOrder.MoveGroup(["m1", "m2", "m3", "a1"], ["m1"], "m2", after: true));
    }

    [Fact]
    public void A_mod_dropped_into_a_category_lands_where_it_was_dropped()
    {
        var sectionOf = new Dictionary<string, string> { ["m1"] = "mods", ["m2"] = "mods", ["a1"] = "category:a", ["a2"] = "category:a" };
        var order = LoadOrder.MoveGroup(["m1", "m2", "a1", "a2"], ["m1"], "a2", after: false);
        sectionOf["m1"] = "category:a"; // the drop also puts it into the category
        Assert.Equal(["m2", "a1", "m1", "a2"], BlockLayout.Compose(order, id => sectionOf[id], Sections));
    }

    [Fact]
    public void Several_mods_move_as_one_group_in_their_current_order()
    {
        // selected in another order than they load: the group keeps the load order
        var moved = LoadOrder.MoveGroup(["m1", "m2", "m3", "m4", "m5"], ["m4", "m2"], "m5", after: true);
        Assert.Equal(["m1", "m3", "m5", "m2", "m4"], moved);
        // a target inside the group changes nothing
        Assert.Equal(["m1", "m2", "m3"], LoadOrder.MoveGroup(["m1", "m2", "m3"], ["m1", "m2"], "m2", after: false));
    }

    [Fact]
    public void A_layer_stays_right_below_its_mod_and_moves_with_it()
    {
        Row[] rows =
        [
            new("m1", Mods, "Alpha"), new("m2", Mods, "Beta"), new("m3", Mods, "Gamma"),
            new("edit2", Mods, "Beta", Parent: "m2"), new("a1", CatA, "Delta"),
        ];
        Assert.Equal(["m1", "m2", "edit2", "m3", "a1"], Arrange(rows, byName: false));
        // the profile moved Beta to the end of MODS (layers are not part of the stored order)
        Row[] moved = [rows[0], rows[2], rows[1], rows[3], rows[4]];
        Assert.Equal(["m1", "m3", "m2", "edit2", "a1"], Arrange(moved, byName: false));
        // a layer whose mod sits in another section is shown on its own
        Row[] apart = [new("m1", Mods, "Alpha"), new("edit", CatA, "Alpha", Parent: "m1")];
        Assert.Equal(["m1", "edit"], Arrange(apart, byName: false));
    }

    [Fact]
    public void Sort_by_name_sorts_only_its_own_section()
    {
        var sectionOf = new Dictionary<string, string> { ["c"] = "collection:x", ["z"] = "mods", ["b"] = "mods", ["y"] = "category:a", ["a"] = "category:a" };
        var names = new Dictionary<string, string> { ["c"] = "C", ["z"] = "Zeta", ["b"] = "beta", ["y"] = "Y", ["a"] = "A" };
        var sorted = LoadOrder.SortSection(["c", "z", "b", "y", "a"], id => sectionOf[id], "mods", id => names[id], _ => "1.0", Names);
        Assert.Equal(["c", "b", "z", "y", "a"], sorted);
        var cat = LoadOrder.SortSection(["c", "z", "b", "y", "a"], id => sectionOf[id], "category:a", id => names[id], _ => "1.0", Names);
        Assert.Equal(["c", "z", "b", "a", "y"], cat);
    }

    [Fact]
    public void A_new_mod_lands_at_the_end_of_MODS_before_the_categories()
    {
        // a mod not in the profile yet comes last from the overview; its section is MODS without category
        Row[] rows = [new("a1", CatA, "Armor"), new("m2", Mods, "Zeta"), new("m1", Mods, "Beta"), new("new", Mods, "Aardvark")];
        Assert.Equal(["m2", "m1", "new", "a1"], Arrange(rows, byName: false));
    }

    [Fact]
    public void An_old_library_keeps_its_order()
    {
        // what the list showed so far: by name inside MODS and categories, a layer right below its mod
        Row[] rows =
        [
            new("m3", Mods, "gamma"), new("a2", CatA, "Weapons B"), new("m1", Mods, "Alpha"),
            new("edit1", Mods, "Alpha", Parent: "m1"), new("a1", CatA, "Weapons A"), new("m2", Mods, "Beta"),
        ];
        var before = Arrange(rows, byName: true);
        Assert.Equal(["m1", "edit1", "m2", "m3", "a1", "a2"], before);
        // a custom profile already holds that order (the earlier version wrote it): the free order shows the same
        var stored = rows.OrderBy(r => before.IndexOf(r.Id)).ToList();
        Assert.Equal(before, Arrange(stored, byName: false));

        // every profile of a library from before is shown by name once more; a set-up library keeps its list
        Assert.True(LoadOrder.PendingTakeOver(null, ["p1", "p2"]).SetEquals(["p1", "p2"]));
        Assert.True(LoadOrder.PendingTakeOver(LoadOrder.FormatTakeOver(["p2", "p3"]), ["p1", "p2"]).SetEquals(["p2", "p3"]));
        Assert.Empty(LoadOrder.PendingTakeOver(LoadOrder.FormatTakeOver([]), ["p1", "p2"]));
    }

    private static readonly Dictionary<string, string> KeySections = new()
    {
        ["c1"] = "collection:x", ["m1"] = "mods", ["m2"] = "mods", ["m3"] = "mods", ["m4"] = "mods",
        ["a1"] = "category:a", ["a2"] = "category:a",
    };
    private static readonly string[] KeyOrder = ["c1", "m1", "m2", "m3", "m4", "a1", "a2"];

    private static List<string> Key(string[] moved, MoveStep step, string[]? order = null) =>
        LoadOrder.MoveInSection(order ?? KeyOrder, moved, id => KeySections[id], step);

    [Fact]
    public void Ctrl_up_and_down_move_a_mod_by_one_row_inside_its_section()
    {
        Assert.Equal(["c1", "m2", "m1", "m3", "m4", "a1", "a2"], Key(["m2"], MoveStep.Up));
        Assert.Equal(["c1", "m1", "m3", "m2", "m4", "a1", "a2"], Key(["m2"], MoveStep.Down));
    }

    [Fact]
    public void A_key_move_never_crosses_a_section_boundary()
    {
        Assert.Equal(KeyOrder, Key(["m1"], MoveStep.Up));
        Assert.Equal(KeyOrder, Key(["m4"], MoveStep.Down));
        Assert.Equal(KeyOrder, Key(["a1"], MoveStep.Up));
        Assert.Equal(KeyOrder, Key(["a2"], MoveStep.Down));
    }

    [Fact]
    public void Several_mods_step_together_and_a_block_at_the_edge_stays()
    {
        Assert.Equal(["c1", "m2", "m1", "m4", "m3", "a1", "a2"], Key(["m1", "m3"], MoveStep.Down));
        Assert.Equal(["c1", "m1", "m3", "m2", "m4", "a1", "a2"], Key(["m1", "m3"], MoveStep.Up, ["c1", "m2", "m1", "m3", "m4", "a1", "a2"]));
        // the top mod cannot go further up, the other one still does
        Assert.Equal(["c1", "m1", "m3", "m2", "m4", "a1", "a2"], Key(["m1", "m3"], MoveStep.Up));
    }

    [Fact]
    public void Ctrl_home_and_end_go_to_the_start_or_end_of_the_section()
    {
        Assert.Equal(["c1", "m3", "m1", "m2", "m4", "a1", "a2"], Key(["m3"], MoveStep.ToStart));
        Assert.Equal(["c1", "m1", "m3", "m4", "m2", "a1", "a2"], Key(["m2"], MoveStep.ToEnd));
        // two mods of different sections each go to the end of their own
        Assert.Equal(["c1", "m1", "m3", "m4", "m2", "a2", "a1"], Key(["m2", "a1"], MoveStep.ToEnd));
    }

    [Fact]
    public void A_key_move_with_nothing_to_move_changes_nothing()
    {
        Assert.Equal(KeyOrder, Key([], MoveStep.Up));
        Assert.Equal(KeyOrder, Key(["unknown"], MoveStep.ToStart));
    }

    [Fact]
    public void Only_archives_directly_in_archive_pc_mod_follow_the_load_order()
    {
        Assert.True(LoadOrder.IsOrderedArchive("archive/pc/mod/Texture.archive"));
        Assert.True(LoadOrder.IsOrderedArchive("Archive/PC/Mod/Texture.ARCHIVE"));
        Assert.False(LoadOrder.IsOrderedArchive("archive/pc/mod/sub/Texture.archive"));
        Assert.False(LoadOrder.IsOrderedArchive("archive/pc/mod/readme.txt"));
        Assert.False(LoadOrder.IsOrderedArchive("bin/x64/plugins/x.archive"));
        Assert.False(LoadOrder.IsOrderedArchive("r6/scripts/mod.reds"));
    }
}
