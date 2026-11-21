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
