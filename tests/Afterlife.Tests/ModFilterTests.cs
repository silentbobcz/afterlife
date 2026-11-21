using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

/// <summary>The filter panel of the mod list.</summary>
public class ModFilterTests
{
    private static HashSet<string> F(params string[] keys) => new(keys, StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, FilterState> S(params (string Key, FilterState State)[] s) => s.ToDictionary(x => x.Key, x => x.State);

    [Fact]
    public void No_filter_shows_everything()
    {
        Assert.True(ModFilter.Matches(F(ModFilter.On), S(), matchAll: true));
        Assert.True(ModFilter.Matches(F(ModFilter.On), S((ModFilter.Off, FilterState.Off)), matchAll: false));
    }

    [Fact]
    public void Match_all_needs_every_included_facet()
    {
        var states = S((ModFilter.On, FilterState.Include), (ModFilter.Update, FilterState.Include));
        Assert.True(ModFilter.Matches(F(ModFilter.On, ModFilter.Update), states, matchAll: true));
        Assert.False(ModFilter.Matches(F(ModFilter.On), states, matchAll: true));
    }

    [Fact]
    public void Match_any_needs_one_included_facet()
    {
        var states = S((ModFilter.Conflict, FilterState.Include), (ModFilter.Missing, FilterState.Include));
        Assert.True(ModFilter.Matches(F(ModFilter.Conflict), states, matchAll: false));
        Assert.False(ModFilter.Matches(F(ModFilter.On), states, matchAll: false));
    }

    [Fact]
    public void Excluded_facet_always_hides()
    {
        var states = S((ModFilter.Update, FilterState.Include), (ModFilter.Core, FilterState.Exclude));
        Assert.False(ModFilter.Matches(F(ModFilter.Update, ModFilter.Core), states, matchAll: false));
        Assert.True(ModFilter.Matches(F(ModFilter.Update, ModFilter.Mods), states, matchAll: false));
        // only an exclusion: everything else stays
        Assert.True(ModFilter.Matches(F(ModFilter.Mods), S((ModFilter.Core, FilterState.Exclude)), matchAll: true));
    }

    [Fact]
    public void A_click_cycles_include_exclude_off()
    {
        Assert.Equal(FilterState.Include, ModFilter.Next(FilterState.Off));
        Assert.Equal(FilterState.Exclude, ModFilter.Next(FilterState.Include));
        Assert.Equal(FilterState.Off, ModFilter.Next(FilterState.Exclude));
    }

    [Fact]
    public void Facets_come_from_the_overview_block_and_tags()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/version.dll", "v"));
        var mod = t.ImportZip("Lua mod", ("bin/x64/plugins/cyber_engine_tweaks/mods/Lua/init.lua", "x"));
        t.C.SetEnabled(mod.PackageId, true);
        var o = OverviewBuilder.Build(t.C);
        var row = o.Mods.Single(m => m.Package.PackageId == mod.PackageId);
        var f = ModFilter.Facets(row, ModBlock.Mods, ["CET"]);
        Assert.Contains(ModFilter.On, f);
        Assert.Contains(ModFilter.Missing, f); // CET is in the library but off
        Assert.Contains(ModFilter.Mods, f);
        Assert.Contains(ModFilter.Tag("cet"), f); // tags compare without case
        Assert.DoesNotContain(ModFilter.Off, f);
        var off = ModFilter.Facets(o.Mods.Single(m => m.Package.PackageId == cet.PackageId), ModBlock.Core, []);
        Assert.Contains(ModFilter.Off, off);
        Assert.Contains(ModFilter.Core, off);
    }
}

public class ModSortTests
{
    private sealed record Row(string Id, int Block, bool Core, ModOverview O);

    private static Row R(string id, string name, int block, bool core = false, bool wanted = true, long size = 1, int addedDaysAgo = 0,
        ModState state = ModState.Deployed, string? layerOf = null)
    {
        var p = new PackageManifest(id, layerOf is null ? "mod-" + id : "mod-layer-" + id, name, "1.0", PackageOrigin.Manual, layerOf, "d",
            [new PackageFile("a", "archive/pc/mod/" + id + ".archive", "h", size)], [], [], null, null, null, null, null,
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).AddDays(-addedDaysAgo));
        return new Row(id, block, core, new ModOverview(p, wanted, wanted, wanted, state, "", Tone.Neutral, false, "", [], [], [], []));
    }

    private static string[] Ids(IEnumerable<Row> rows) => rows.Select(r => r.Id).ToArray();

    private static IReadOnlyList<Row> View(IReadOnlyList<Row> rows, ListSort sort) => ModSort.View(rows, sort, r => r.O, r => r.Block, r => r.Core);

    // core block, a collection in the curator's order, then MODS by name
    private static readonly Row[] LoadOrder =
    [
        R("core2", "Zeta core", -1, core: true), R("core1", "Alpha core", -1, core: true),
        R("c1", "Mango", 0, size: 5, addedDaysAgo: 9), R("c2", "Apple", 0, size: 50, addedDaysAgo: 1, wanted: false, state: ModState.Off),
        R("m1", "Banana", 1, size: 7, addedDaysAgo: 3), R("m1x", "└ my edits", 1, layerOf: "mod-m1"),
        R("m2", "Cherry", 1, size: 900, addedDaysAgo: 0, state: ModState.Conflict), R("m3", "Date", 1, size: 2, addedDaysAgo: 5, wanted: false, state: ModState.Off),
    ];

    [Fact]
    public void Load_order_is_returned_untouched()
    {
        Assert.Same(LoadOrder, View(LoadOrder, ListSort.LoadOrder));
    }

    [Theory]
    [InlineData(ListSort.Name, "core2,core1,c2,c1,m1,m1x,m2,m3")]
    [InlineData(ListSort.Added, "core2,core1,c2,c1,m2,m1,m1x,m3")]
    [InlineData(ListSort.Size, "core2,core1,c2,c1,m2,m1,m1x,m3")]
    [InlineData(ListSort.State, "core2,core1,c1,c2,m2,m1,m1x,m3")]
    public void Sorting_stays_inside_blocks_keeps_core_and_keeps_a_layer_below_its_mod(ListSort sort, string expected)
    {
        Assert.Equal(expected, string.Join(",", Ids(View(LoadOrder, sort))));
    }

    [Fact]
    public void A_sorted_view_is_the_same_rows_and_leaves_the_load_order_alone()
    {
        var before = Ids(LoadOrder);
        foreach (var sort in Enum.GetValues<ListSort>())
        {
            var shown = View(LoadOrder, sort);
            Assert.Equal(before.Order(), Ids(shown).Order());
            Assert.Equal(before, Ids(LoadOrder)); // the list in load order is what every reordering reads
        }
    }

    [Fact]
    public void The_button_walks_through_every_sort_and_back()
    {
        var s = ListSort.LoadOrder;
        var seen = new List<ListSort>();
        do { seen.Add(s); s = ModSort.Next(s); } while (s != ListSort.LoadOrder);
        Assert.Equal(Enum.GetValues<ListSort>(), seen);
    }
}
