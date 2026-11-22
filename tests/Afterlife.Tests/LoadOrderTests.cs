using Afterlife.Core;

namespace Afterlife.Tests;

/// <summary>The free load order inside MODS and its categories: moves, groups, layers, A→Z, new mods, old libraries.</summary>
public class LoadOrderTests
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
}
