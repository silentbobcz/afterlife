using Afterlife.Core;

namespace Afterlife.Tests;

/// <summary>The load list: reading, writing, finding its mods in the library and the plan of an import.</summary>
public class LoadListTests
{
    private static readonly DateTimeOffset When = new(2026, 11, 22, 18, 40, 0, TimeSpan.Zero);
    private static readonly LoadListHeader Header = new("Main", When, "1.0.0-beta.2", false, false);

    private static LoadListDocument Parse(string text)
    {
        var r = LoadList.Read(text);
        Assert.True(r.Document is not null, r.Error);
        return r.Document!;
    }

    private static string ListOf(params string[] lines) => string.Join("\n", new[] { "AFTERLIFE LOAD LIST 1" }.Concat(lines));

    // ---------------- reading and writing ----------------

    [Fact]
    public void Write_then_read_gives_the_same_list()
    {
        List<LoadListSection> sections =
        [
            new(0, LoadListSectionKind.Core, "", [new(0, true, "Cyber Engine Tweaks", "1.35.0", "107", "91234")]),
            new(0, LoadListSectionKind.Collection, "Night City | Glow", [new(0, true, "Better Lighting", "2.1", "1234", "56789")], "nc-glow", 7, true),
            new(0, LoadListSectionKind.Mods, "",
            [
                new(0, true, "Immersive HUD", "1.8", "4410"),
                new(0, false, "Old Map Colours", "0.9", null, null, "OldMapColours.zip", "9b2c0f6a1d3e4b5c6d7e8f9012345678"),
                new(0, true, "Appearance Menu Mod", "2.6.1", "790", "62233") { Layers = [new LoadListLayer("Appearance Menu Mod (local edits)", true)] },
                new(0, true, "No Version Mod"),
            ]),
            new(0, LoadListSectionKind.Category, "Weapons", [new(0, false, "Katana | Pack", "4.0", "6200", "63222")]),
            new(0, LoadListSectionKind.Category, "Empty", []),
        ];
        var text = LoadList.Write(Header, sections);
        var doc = Parse(text);

        Assert.Equal("1.0.0-beta.2", doc.MadeBy);
        Assert.Equal(sections.Count, doc.Sections.Count);
        for (var i = 0; i < sections.Count; i++)
        {
            var a = sections[i];
            var b = doc.Sections[i];
            Assert.Equal((a.Kind, a.Name, a.Slug, a.Revision, a.Locked), (b.Kind, b.Name, b.Slug, b.Revision, b.Locked));
            Assert.Equal(a.Mods.Select(Key), b.Mods.Select(Key));
        }
        // layers are notes only
        Assert.Contains("#   └ + Appearance Menu Mod (local edits)", text);
        Assert.All(doc.Mods, m => Assert.Empty(m.Layers));
    }

    private static (bool, string, string?, string?, string?, string?, string?) Key(LoadListMod m) =>
        (m.Enabled, m.Name, m.Version, m.NexusModId, m.NexusFileId, m.FileName, m.Md5);

    [Fact]
    public void A_profile_in_the_default_order_says_so_in_a_comment()
    {
        var text = LoadList.Write(Header with { DefaultOrder = true }, []);
        Assert.Contains("# " + LoadList.DefaultOrderComment, text);
        Assert.Empty(Parse(text).Sections);
    }

    [Fact]
    public void A_hand_written_minimal_list_parses()
    {
        var doc = Parse("AFTERLIFE LOAD LIST 1\n+ Better Lighting\n- Katana Pack\n\n# a note\n");
        var s = Assert.Single(doc.Sections);
        Assert.Equal(LoadListSectionKind.Mods, s.Kind);
        Assert.Equal(["Better Lighting", "Katana Pack"], s.Mods.Select(m => m.Name));
        Assert.True(s.Mods[0].Enabled);
        Assert.False(s.Mods[1].Enabled);
        Assert.Null(s.Mods[0].Version);
        Assert.Equal(2, s.Mods[0].Line);
    }

    [Fact]
    public void Fields_are_read_and_unknown_ones_ignored()
    {
        var doc = Parse(ListOf("@mods", "+ A \\| B | 2.1 | nexus=12/34 | file=a.zip | md5=ABC | future=yes", "+ C | nexus=55"));
        var a = doc.Mods.First();
        Assert.Equal(("A | B", "2.1", "12", "34", "a.zip", "ABC"), (a.Name, a.Version, a.NexusModId, a.NexusFileId, a.FileName, a.Md5));
        var c = doc.Mods.Last();
        Assert.Equal(("55", (string?)null, (string?)null), (c.NexusModId, c.NexusFileId, c.Version));
    }

    [Theory]
    [InlineData("AFTERLIFE LOAD LIST 1\n@mods\nBetter Lighting", "Line 3: a mod line starts with + or -. Nothing was loaded.")]
    [InlineData("AFTERLIFE LOAD LIST 1\n+ A\n  └ + A (local edits)", "Line 3: a mod line starts with + or -. Nothing was loaded.")]
    [InlineData("AFTERLIFE LOAD LIST 1\n\n@group Weapons", "Line 3: unknown section @group; use @core, @collection, @mods or @category. Nothing was loaded.")]
    [InlineData("AFTERLIFE LOAD LIST 1\n@category", "Line 2: @category needs a name. Nothing was loaded.")]
    [InlineData("AFTERLIFE LOAD LIST 1\n@collection | slug=x", "Line 2: @collection needs a name. Nothing was loaded.")]
    [InlineData("AFTERLIFE LOAD LIST 1\n+ | 2.1", "Line 2: a mod line needs a name after + or -. Nothing was loaded.")]
    [InlineData("", "This is not an AFTERLIFE load list. Nothing was loaded.")]
    [InlineData("+ Better Lighting", "This is not an AFTERLIFE load list. Nothing was loaded.")]
    [InlineData("AFTERLIFE LOAD LIST x", "This is not an AFTERLIFE load list. Nothing was loaded.")]
    [InlineData("AFTERLIFE LOAD LIST 2\n+ A", "This list was made by a newer AFTERLIFE (format 2). Update AFTERLIFE to load it.")]
    public void Every_error_has_its_message(string text, string message)
    {
        var r = LoadList.Read(text);
        Assert.Null(r.Document);
        Assert.Equal(message, r.Error);
    }

    [Fact]
    public void The_file_name_has_the_profile_and_the_date()
    {
        Assert.Equal("afterlife-order-Main-2026-11-22.txt", LoadList.SuggestedFileName("Main", new DateTime(2026, 11, 22)));
        Assert.Equal("afterlife-order-a_b-2026-11-22.txt", LoadList.SuggestedFileName("a/b", new DateTime(2026, 11, 22)));
    }

    // ---------------- matching ----------------

    private static LoadListPackage Pkg(string id, string name, string version = "1.0", string section = "mods", bool on = true,
        string? nexus = null, string? file = null, string? archive = null, string? md5 = null, bool core = false, bool layer = false, int day = 1) =>
        new(id, name, version, core ? LoadListPlanner.CoreSection : section, on, core, layer, nexus, file, archive, md5, new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Matching_tries_nexus_then_md5_then_file_then_name_and_version_then_name()
    {
        var library = new List<LoadListPackage>
        {
            Pkg("byname", "Mod", "9.0"),
            Pkg("byversion", "Mod", "2.0"),
            Pkg("byfile", "Other", archive: "mod.zip"),
            Pkg("bymd5", "Other2", md5: "abc"),
            Pkg("bynexus", "Other3", nexus: "1", file: "2"),
        };
        LoadListMatch? M(LoadListMod line) => LoadListPlanner.Match(line, library);

        var full = new LoadListMod(1, true, "Mod", "2.0", "1", "2", "mod.zip", "ABC");
        Assert.Equal(("bynexus", LoadListMatchKind.NexusFile), (M(full)!.Package.PackageId, M(full)!.Kind));
        var noNexus = full with { NexusModId = null, NexusFileId = null };
        Assert.Equal(("bymd5", LoadListMatchKind.Md5), (M(noNexus)!.Package.PackageId, M(noNexus)!.Kind));
        var noMd5 = noNexus with { Md5 = null };
        Assert.Equal(("byfile", LoadListMatchKind.FileName), (M(noMd5)!.Package.PackageId, M(noMd5)!.Kind));
        var noFile = noMd5 with { FileName = null };
        Assert.Equal(("byversion", LoadListMatchKind.NameAndVersion), (M(noFile)!.Package.PackageId, M(noFile)!.Kind));
        var otherVersion = noFile with { Version = "3.0" };
        var byName = M(otherVersion)!;
        Assert.Equal(LoadListMatchKind.NameOnly, byName.Kind);
        Assert.True(byName.OtherVersion);
        Assert.Null(M(new LoadListMod(1, true, "Nothing like it")));
    }

    [Fact]
    public void Several_versions_prefer_the_one_switched_on_then_the_newest()
    {
        var library = new List<LoadListPackage> { Pkg("old", "Mod", "1.0", on: true, day: 1), Pkg("new", "Mod", "2.0", on: false, day: 5) };
        Assert.Equal("old", LoadListPlanner.Match(new LoadListMod(1, true, "Mod"), library)!.Package.PackageId);
        var off = library.Select(p => p with { Enabled = false }).ToList();
        Assert.Equal("new", LoadListPlanner.Match(new LoadListMod(1, true, "Mod"), off)!.Package.PackageId);
    }

    [Fact]
    public void A_nexus_mod_id_without_file_matches_within_that_mod_and_never_another_mods_name()
    {
        var library = new List<LoadListPackage> { Pkg("same-name-other-mod", "Mod", nexus: "999"), Pkg("ours", "Renamed", "3.0", nexus: "12", file: "77") };
        var m = LoadListPlanner.Match(new LoadListMod(1, true, "Mod", "1.0", "12"), library)!;
        Assert.Equal(("ours", LoadListMatchKind.NexusMod), (m.Package.PackageId, m.Kind));
        Assert.Null(LoadListPlanner.Match(new LoadListMod(1, true, "Mod", null, "13"), library));
    }

    [Fact]
    public void Layers_are_never_matched()
    {
        var library = new List<LoadListPackage> { Pkg("layer", "AMM (local edits)", layer: true) };
        Assert.Null(LoadListPlanner.Match(new LoadListMod(1, true, "AMM (local edits)"), library));
    }

    // ---------------- the plan ----------------

    private static readonly LoadListOptions Defaults = new();

    private static int _ids;
    private static string NewId() => "new" + Interlocked.Increment(ref _ids);

    private static LoadListPlan Plan(string text, IReadOnlyList<LoadListPackage> library, BlockLayout? layout = null,
        IReadOnlyList<LoadListCollection>? collections = null, bool custom = true, LoadListOptions? options = null) =>
        LoadListPlanner.Build(Parse(text), library, layout ?? BlockLayout.Empty, collections ?? [], custom, options ?? Defaults, NewId);

    /// <summary>The library as it is after the plan was written: the window's view of the next import.</summary>
    private static List<LoadListPackage> After(LoadListPlan plan, IReadOnlyList<LoadListPackage> library)
    {
        var byId = library.ToDictionary(p => p.PackageId);
        var on = plan.SwitchOn.ToHashSet();
        var off = plan.SwitchOff.ToHashSet();
        LoadListPackage Next(LoadListPackage p)
        {
            var enabled = on.Contains(p.PackageId) || (p.Enabled && !off.Contains(p.PackageId));
            var section = p.IsCore || p.IsLayer || p.Section.StartsWith(BlockLayout.CollectionPrefix, StringComparison.Ordinal) ? p.Section
                : plan.Layout.CategoryIdOf(p.PackageId) is { } c ? BlockLayout.CategoryKey(c) : BlockLayout.ModsBlock;
            return p with { Enabled = enabled, Section = section };
        }
        return library.Where(p => p.IsCore || p.IsLayer).Concat(plan.Order.Select(id => byId[id])).Select(Next).ToList();
    }

    private static readonly List<LoadListPackage> Library =
    [
        Pkg("cet", "Cyber Engine Tweaks", "1.35.0", nexus: "107", file: "91234", core: true),
        Pkg("a", "Alpha"),
        Pkg("b", "Bravo"),
        Pkg("c", "Charlie", on: false),
        Pkg("d", "Delta"),
    ];

    [Fact]
    public void The_same_list_imported_twice_gives_an_empty_plan_the_second_time()
    {
        var text = ListOf("@core", "+ Cyber Engine Tweaks | 1.35.0 | nexus=107/91234", "@mods", "+ Delta", "+ Charlie",
            "@category Weapons", "- Alpha", "@collection Night City Glow | slug=gone", "+ Bravo", "+ Missing Mod | nexus=5/6");
        var first = Plan(text, Library);
        Assert.False(first.IsEmpty);
        Assert.Equal(["Weapons", "Night City Glow (collection)"], first.NewCategories);

        var layoutAfter = first.Layout;
        var second = LoadListPlanner.Build(Parse(text), After(first, Library), layoutAfter, [], true, Defaults, NewId);
        Assert.True(second.IsEmpty);
        Assert.Empty(second.Moved);
        Assert.Empty(second.NewCategories);
        Assert.Single(second.Missing);
    }

    [Fact]
    public void Case1_a_missing_category_is_created_where_the_list_has_it()
    {
        var layout = BlockLayout.Empty.WithCategories([new ModCategory("x", "UI"), new ModCategory("y", "Clothes")]);
        var plan = Plan(ListOf("@category UI", "+ Alpha", "@category Weapons", "+ Bravo", "@category Clothes", "+ Delta"), Library, layout);
        Assert.Equal(["UI", "Weapons", "Clothes"], plan.Layout.Categories.Select(c => c.Name));
        Assert.Equal(["Weapons"], plan.NewCategories);
        var weapons = plan.Layout.Categories[1].Id;
        Assert.Equal(weapons, plan.Layout.CategoryIdOf("b"));
        Assert.Equal("x", plan.Layout.CategoryIdOf("a"));
    }

    [Fact]
    public void Case2_a_missing_collection_becomes_a_category_with_the_mods_found()
    {
        var plan = Plan(ListOf("@collection Night City Glow | slug=nc-glow | revision=7", "+ Alpha", "+ Nowhere"), Library);
        var section = Assert.Single(plan.MissingCollections);
        Assert.Equal(("nc-glow", 7), (section.Slug, section.Revision));
        var category = Assert.Single(plan.Layout.Categories);
        Assert.Equal("Night City Glow (collection)", category.Name);
        Assert.Equal(category.Id, plan.Layout.CategoryIdOf("a"));

        // nothing found for it: no empty category
        var empty = Plan(ListOf("@collection Gone", "+ Nowhere", "@mods", "+ Alpha"), Library);
        Assert.Empty(empty.Layout.Categories);
        Assert.Single(empty.MissingCollections);
    }

    [Fact]
    public void Case3_mods_not_in_the_library_are_listed_and_the_rest_is_imported()
    {
        var plan = Plan(ListOf("+ Delta", "+ Katana Pack | 4.0 | nexus=6200/63222", "+ Alpha"), Library);
        var missing = Assert.Single(plan.Missing);
        Assert.Equal(("Katana Pack", "6200", 2 + 1), (missing.Name, missing.NexusModId, missing.Line));
        Assert.Equal(["d", "a", "b", "c"], plan.Order);
    }

    [Fact]
    public void Case4_mods_not_in_the_list_stay_at_the_end_of_their_section_or_go_off_when_asked()
    {
        var plan = Plan(ListOf("+ Delta", "+ Bravo"), Library);
        Assert.Equal(["d", "b", "a", "c"], plan.Order);
        Assert.Equal(["Alpha"], plan.NotInList); // Charlie is off, the core mod is not listed by a list without @core
        Assert.Empty(plan.SwitchOff);

        var strict = Plan(ListOf("+ Delta", "+ Bravo"), Library, options: Defaults with { SwitchOffOthers = true });
        Assert.Equal(["a"], strict.SwitchOff);

        var withCore = Plan(ListOf("@core", "@mods", "+ Delta", "+ Bravo"), Library, options: Defaults with { SwitchOffOthers = true });
        Assert.Equal(["cet", "a"], withCore.SwitchOff);
    }

    [Fact]
    public void Case5_a_locked_collection_keeps_the_curators_order_unless_unlocked()
    {
        var library = new List<LoadListPackage> { Pkg("x1", "First", section: "collection:glow"), Pkg("x2", "Second", section: "collection:glow"), Pkg("m", "Mine") };
        var text = ListOf("@collection Glow | slug=glow | locked", "+ Second", "+ First", "@mods", "+ Mine");
        var locked = Plan(text, library, collections: [new LoadListCollection("glow", "Glow", true)]);
        Assert.Equal(["x1", "x2", "m"], locked.Order);
        Assert.Equal(["Glow"], locked.LockedKept);
        Assert.True(locked.IsEmpty);

        var unlocked = Plan(text, library, collections: [new LoadListCollection("glow", "Glow", true)], options: Defaults with { UnlockCollections = true });
        Assert.Equal(["x2", "x1", "m"], unlocked.Order);
        Assert.Equal(["glow"], unlocked.Unlock);
    }

    [Fact]
    public void Case6_a_mod_listed_under_a_collection_it_is_not_part_of_stays_where_the_library_has_it()
    {
        var library = new List<LoadListPackage> { Pkg("x1", "First", section: "collection:glow"), Pkg("m", "Mine") };
        var plan = Plan(ListOf("@collection Glow | slug=glow", "+ First", "+ Mine", "@category Weapons", "+ First"), library,
            collections: [new LoadListCollection("glow", "Glow", false)]);
        Assert.Contains(plan.StayOutside, s => s.StartsWith("Mine", StringComparison.Ordinal));
        Assert.Null(plan.Layout.CategoryIdOf("m"));
        Assert.Contains(plan.Duplicates, d => d.Name == "First");
    }

    [Fact]
    public void Case7_core_mods_keep_their_place_but_on_and_off_counts()
    {
        var plan = Plan(ListOf("@mods", "- Cyber Engine Tweaks", "+ Alpha", "@core", "+ Shader Cache Fix"), Library);
        Assert.Equal(["cet"], plan.SwitchOff);
        Assert.DoesNotContain("cet", plan.Order);
        Assert.Equal("Shader Cache Fix", Assert.Single(plan.Missing).Name);
    }

    [Fact]
    public void Case9_a_mod_moves_to_the_category_of_the_list()
    {
        var layout = BlockLayout.Empty.WithCategories([new ModCategory("ui", "UI"), new ModCategory("w", "Weapons")]).Assign(["a"], "ui");
        var library = Library.Select(p => p.PackageId == "a" ? p with { Section = "category:ui" } : p).ToList();
        var plan = Plan(ListOf("@category weapons", "+ Alpha"), library, layout);
        Assert.Equal("w", plan.Layout.CategoryIdOf("a"));
        Assert.Equal(["Alpha → Weapons"], plan.CategoryMoves);
        Assert.True(plan.LayoutChanged);
    }

    [Fact]
    public void Case10_a_category_not_in_the_list_stays()
    {
        var layout = BlockLayout.Empty.WithCategories([new ModCategory("ui", "UI")]).Assign(["d"], "ui");
        var library = Library.Select(p => p.PackageId == "d" ? p with { Section = "category:ui" } : p).ToList();
        var plan = Plan(ListOf("@category Weapons", "+ Alpha"), library, layout);
        Assert.Contains(plan.Layout.Categories, c => c.Name == "UI");
        Assert.Equal("ui", plan.Layout.CategoryIdOf("d"));
    }

    [Fact]
    public void Case11_the_first_line_of_a_mod_counts()
    {
        var plan = Plan(ListOf("+ Alpha", "- Alpha"), Library);
        var dup = Assert.Single(plan.Duplicates);
        Assert.Equal(3, dup.Line);
        Assert.Empty(plan.SwitchOff);
    }

    [Fact]
    public void Case12_a_long_category_name_is_cut()
    {
        var plan = Plan(ListOf("@category " + new string('W', 40), "+ Alpha"), Library);
        Assert.Equal(new string('W', 32), Assert.Single(plan.Layout.Categories).Name);
        Assert.Single(plan.CutNames);
    }

    [Fact]
    public void Nothing_found_when_no_mod_of_the_list_is_in_the_library()
    {
        var plan = Plan(ListOf("+ Nowhere", "+ Gone"), Library);
        Assert.True(plan.NothingFound);
        Assert.Equal(2, plan.Missing.Count);
    }

    [Fact]
    public void Case16_sections_the_window_cannot_show_are_noted_and_MODS_stands_at_the_first_part()
    {
        var library = new List<LoadListPackage> { Pkg("x1", "First", section: "collection:glow"), Pkg("a", "Alpha"), Pkg("b", "Bravo") };
        var collections = new[] { new LoadListCollection("glow", "Glow", false) };
        var plan = Plan(ListOf("@category Weapons", "+ Alpha", "@collection Glow | slug=glow", "+ First", "@mods", "+ Bravo"), library, collections: collections);
        Assert.True(plan.SectionOrderNote);
        Assert.Equal(["mods", "collection:glow"], plan.Layout.NormalizedBlocks(["glow"]));
        Assert.Equal(["b", "a", "x1"], BlockLayout.Compose(plan.Order, id => After(plan, library).First(p => p.PackageId == id).Section, plan.Layout.Sections(["glow"])));

        var fine = Plan(ListOf("@collection Glow | slug=glow", "+ First", "@mods", "+ Bravo", "@category Weapons", "+ Alpha"), library, collections: collections);
        Assert.False(fine.SectionOrderNote);
    }

    [Fact]
    public void Case17_a_profile_in_the_default_order_gets_its_own()
    {
        var text = ListOf("+ Alpha", "+ Bravo", "- Charlie", "+ Delta");
        var plan = Plan(text, Library, custom: false);
        Assert.True(plan.TakesOwnOrder);
        Assert.False(plan.IsEmpty);
        Assert.True(Plan(text, Library).IsEmpty);
    }

    [Fact]
    public void Switching_follows_the_list_unless_unticked_and_other_versions_and_name_matches_are_named()
    {
        var plan = Plan(ListOf("+ Charlie | 2.0", "- Delta"), Library);
        Assert.Equal(["c"], plan.SwitchOn);
        Assert.Equal(["d"], plan.SwitchOff);
        Assert.Equal("Charlie", Assert.Single(plan.OtherVersions).Package.Name);
        Assert.Equal(2, plan.NameOnly.Count);

        var orderOnly = Plan(ListOf("+ Charlie", "- Delta"), Library, options: Defaults with { SwitchOnOff = false });
        Assert.Empty(orderOnly.SwitchOn);
        Assert.Empty(orderOnly.SwitchOff);
    }

    [Fact]
    public void Moved_counts_only_the_mods_that_change_place()
    {
        var plan = Plan(ListOf("+ Delta"), Library);
        Assert.Equal(["Delta"], plan.Moved);
    }

    [Fact]
    public void MergeOrder_keeps_unlisted_items_in_their_places_and_inserts_new_ones_after_their_neighbour()
    {
        Assert.Equal(["c", "b", "x", "a"], LoadListPlanner.MergeOrder(["a", "x", "c"], ["c", "b", "a"]));
        Assert.Equal(["n", "a", "b"], LoadListPlanner.MergeOrder(["a", "b"], ["n", "a"]));
        Assert.Equal(["a", "b", "n"], LoadListPlanner.MergeOrder(["a", "b"], ["n"]));
    }
}
