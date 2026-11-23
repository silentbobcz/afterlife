using Afterlife.Core;

namespace Afterlife.Tests;

/// <summary>The NEW block: freshly installed mods, where they load and how they leave it.</summary>
public class NewBlockTests
{
    private static readonly string[] Slugs = ["glow"];

    /// <summary>Section of a mod the way the window decides it: collection, NEW, category or MODS.</summary>
    private static Func<string, string> SectionOf(BlockLayout layout, IReadOnlyDictionary<string, string>? collectionOf = null) => id =>
        collectionOf is not null && collectionOf.TryGetValue(id, out var c) ? c
        : layout.IsNew(id) ? BlockLayout.NewBlock
        : layout.CategoryIdOf(id) is { } cat ? BlockLayout.CategoryKey(cat) : BlockLayout.ModsBlock;

    private const string OldJson = """{"blocks":["mods","collection:glow"],"categories":[{"id":"a","name":"UI"}],"categoryOf":{"p2":"a"}}""";

    [Fact]
    public void A_layout_saved_before_NEW_loads_with_NEW_first_and_empty()
    {
        var layout = BlockLayout.FromJson(OldJson);
        Assert.Empty(layout.NewIds);
        Assert.Equal(["new", "mods", "collection:glow"], layout.NormalizedBlocks(Slugs));
        Assert.Equal("a", layout.CategoryIdOf("p2"));
        // an empty NEW is not written, so the stored layout stays as it was
        Assert.DoesNotContain("newIds", layout.ToJson());
    }

    [Fact]
    public void NEW_members_and_its_place_survive_storage()
    {
        var layout = BlockLayout.FromJson(OldJson).WithBlocks(["mods", "new", "collection:glow"]).AddNew(["p9", "p8"]);
        var back = BlockLayout.FromJson(layout.ToJson());
        Assert.Equal(["mods", "new", "collection:glow"], back.NormalizedBlocks(Slugs));
        Assert.True(back.NewIds.SetEquals(["p8", "p9"]));
    }

    [Fact]
    public void An_empty_NEW_leaves_the_load_order_as_it_was()
    {
        var old = BlockLayout.FromJson(OldJson);
        var collectionOf = new Dictionary<string, string> { ["c1"] = "collection:glow" };
        string[] order = ["p1", "p2", "c1", "p3"];
        var before = BlockLayout.Compose(order, SectionOf(old.WithBlocks(["mods", "collection:glow"]), collectionOf), ["mods", "category:a", "collection:glow"]);
        var after = BlockLayout.Compose(order, SectionOf(old, collectionOf), old.Sections(Slugs));
        Assert.Equal(before, after);
    }

    [Fact]
    public void A_new_mod_in_NEW_on_top_loads_first_and_the_others_keep_their_order()
    {
        var layout = BlockLayout.FromJson(OldJson).AddNew(["fresh"]);
        var collectionOf = new Dictionary<string, string> { ["c1"] = "collection:glow" };
        // a mod not in the profile yet comes at the end of the current order
        var order = BlockLayout.Compose(["p1", "p2", "c1", "p3", "fresh"], SectionOf(layout, collectionOf), layout.Sections(Slugs));
        Assert.Equal(["fresh", "p1", "p3", "p2", "c1"], order);
    }

    [Fact]
    public void NEW_moved_below_MODS_loads_its_mods_after_them()
    {
        var layout = BlockLayout.FromJson(OldJson).AddNew(["fresh"]);
        var moved = layout.WithBlocks(BlockLayout.MoveBefore(layout.NormalizedBlocks(Slugs), "new", "mods", after: true));
        Assert.Equal(["mods", "new", "collection:glow"], moved.NormalizedBlocks(Slugs));
        Assert.Equal(["mods", "category:a", "new", "collection:glow"], moved.Sections(Slugs));
        var order = BlockLayout.Compose(["fresh", "p1", "p2", "p3"], SectionOf(moved), moved.Sections(Slugs));
        Assert.Equal(["p1", "p3", "p2", "fresh"], order);
    }

    [Fact]
    public void Assign_RemoveNew_and_Prune_take_mods_out_of_NEW()
    {
        var layout = BlockLayout.FromJson(OldJson).AddNew(["n1", "n2", "n3", "n4"]);
        Assert.Same(layout, layout.AddNew(["n1"]));
        var toCategory = layout.Assign(["n1"], "a");
        Assert.False(toCategory.IsNew("n1"));
        Assert.Equal("a", toCategory.CategoryIdOf("n1"));
        var toMods = toCategory.Assign(["n2"], null);
        Assert.False(toMods.IsNew("n2"));
        Assert.Null(toMods.CategoryIdOf("n2"));
        var removed = toMods.RemoveNew(["n3"]);
        Assert.Equal("n4", Assert.Single(removed.NewIds));
        var pruned = removed.Prune(new HashSet<string> { "n1", "n2", "n3", "p2" });
        Assert.Empty(pruned.NewIds);
        Assert.Same(pruned, pruned.Prune(new HashSet<string> { "n1", "n2", "n3", "p2" }));
    }
}
