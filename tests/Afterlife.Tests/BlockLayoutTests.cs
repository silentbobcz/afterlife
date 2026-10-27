using Afterlife.Core;

namespace Afterlife.Tests;

/// <summary>P95/P97: blocks, categories and the load order that follows them.</summary>
public class BlockLayoutTests
{
    private static readonly string[] Slugs = ["night-city", "ui-pack"];

    [Fact]
    public void New_collections_go_before_MODS_and_MODS_is_always_there()
    {
        var layout = BlockLayout.Empty.WithBlocks(["mods", "collection:ui-pack", "collection:gone"]);
        Assert.Equal(["collection:night-city", "mods", "collection:ui-pack"], layout.NormalizedBlocks(Slugs));
        Assert.Equal(["collection:night-city", "collection:ui-pack", "mods"], BlockLayout.Empty.NormalizedBlocks(Slugs));
    }

    [Fact]
    public void Categories_follow_the_mods_without_category_inside_MODS()
    {
        var layout = BlockLayout.Empty.WithBlocks(["mods", "collection:night-city"])
            .WithCategories([new ModCategory("a", "UI"), new ModCategory("b", "Weapons")]);
        Assert.Equal(["collection:ui-pack", "mods", "category:a", "category:b", "collection:night-city"], layout.Sections(Slugs));
    }

    [Fact]
    public void Compose_sorts_by_section_and_keeps_the_order_inside_a_section()
    {
        var sections = new[] { "collection:x", "mods", "category:a" };
        var sectionOf = new Dictionary<string, string> { ["p1"] = "category:a", ["p2"] = "mods", ["p3"] = "collection:x", ["p4"] = "mods", ["p5"] = "category:a" };
        var order = BlockLayout.Compose(["p1", "p2", "p3", "p4", "p5"], id => sectionOf[id], sections);
        Assert.Equal(["p3", "p2", "p4", "p1", "p5"], order);
    }

    [Fact]
    public void Assign_and_removing_a_category_send_mods_back_to_MODS()
    {
        var layout = BlockLayout.Empty.WithCategories([new ModCategory("a", "UI")]).Assign(["p1", "p2"], "a");
        Assert.Equal("a", layout.CategoryIdOf("p1"));
        Assert.Null(layout.Assign(["p1"], null).CategoryIdOf("p1"));
        var removed = layout.WithCategories([]);
        Assert.Null(removed.CategoryIdOf("p2"));
        Assert.Empty(removed.CategoryOf);
    }

    [Fact]
    public void MoveBefore_moves_one_item_and_ignores_unknown_targets()
    {
        string[] list = ["a", "b", "c", "d"];
        Assert.Equal(["a", "d", "b", "c"], BlockLayout.MoveBefore(list, "d", "b", after: false));
        Assert.Equal(["b", "c", "a", "d"], BlockLayout.MoveBefore(list, "a", "c", after: true));
        Assert.Equal(list, BlockLayout.MoveBefore(list, "a", "zzz", after: false));
    }

    [Fact]
    public void Layout_survives_storage_and_bad_json_gives_an_empty_layout()
    {
        var layout = BlockLayout.Empty.WithBlocks(["mods", "collection:x"])
            .WithCategories([new ModCategory("a", "UI")]).Assign(["p1"], "a");
        var back = BlockLayout.FromJson(layout.ToJson());
        Assert.Equal(layout.Blocks, back.Blocks);
        Assert.Equal(layout.Categories, back.Categories);
        Assert.Equal("a", back.CategoryIdOf("p1"));
        Assert.Same(BlockLayout.Empty, BlockLayout.FromJson("{not json"));
        Assert.Same(BlockLayout.Empty, BlockLayout.FromJson(null));
    }

    [Fact]
    public void Prune_forgets_deleted_mods()
    {
        var layout = BlockLayout.Empty.WithCategories([new ModCategory("a", "UI")]).Assign(["p1", "p2"], "a");
        var pruned = layout.Prune(new HashSet<string> { "p1" });
        Assert.Equal(["p1"], pruned.CategoryOf.Keys);
        Assert.Same(pruned, pruned.Prune(new HashSet<string> { "p1" }));
    }
}
