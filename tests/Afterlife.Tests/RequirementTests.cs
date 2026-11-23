using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;


/// <summary>The core table and the dependents of a framework.</summary>
public class CoreModTests
{
    [Fact]
    public void Core_table_lists_missing_frameworks_with_nexus_links()
    {
        using var t = new TestEnv();
        t.Write("red4ext/RED4ext.dll", "fw");
        var o = OverviewBuilder.Build(t.C);
        Assert.Equal(FrameworkCatalog.Contracts.Count, o.CoreMods.Count);
        var red = o.CoreMods.Single(c => c.Capability == Capabilities.Red4ext);
        Assert.Equal(CoreSource.Manual, red.Source);
        var cet = o.CoreMods.Single(c => c.Capability == Capabilities.Cet);
        Assert.Equal(CoreSource.Missing, cet.Source);
        Assert.Equal("https://www.nexusmods.com/cyberpunk2077/mods/107", cet.NexusUrl);
        Assert.Equal("OPTIONAL", o.CoreMods.Single(c => c.Capability == Capabilities.ModSettings).SourceLabel);
    }

    [Fact]
    public void Dependents_of_a_core_mod_are_found_and_can_be_switched_off()
    {
        using var t = new TestEnv();
        var m = t.ImportZip("NeedsCet", ("bin/x64/plugins/cyber_engine_tweaks/mods/needs/init.lua", "x"));
        t.C.SetEnabled(m.PackageId, true);
        var deps = t.C.DependentsOf([Capabilities.Cet]);
        Assert.Single(deps);
        Assert.Equal("NeedsCet", deps[0].Name);
        t.C.DeactivateEverywhere([m.PackageId]);
        Assert.False(t.C.SelectedProfile.IsEnabled(m.PackageId));
        Assert.Empty(t.C.DependentsOf([Capabilities.Cet]));
    }

    [Fact]
    public void Cet_and_red4ext_archives_import_without_a_guard()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/version.dll", "loader"));
        Assert.Contains(Capabilities.Cet, cet.Provides);
        var r4 = t.ImportZip("RED4ext", ("red4ext/RED4ext.dll", "dll"), ("bin/x64/winmm.dll", "loader"));
        Assert.Contains(Capabilities.Red4ext, r4.Provides);
    }
}

/// <summary>Requirements read from the files and scripts of a mod.</summary>
public class RequirementSourceTests
{
    [Fact]
    public void Requirements_are_read_from_script_content()
    {
        var reqs = FrameworkCatalog.InferFromContent(
        [
            (GamePath.Parse("r6/scripts/X/a.reds"), "module X\nimport Codeware.*\n"),
            (GamePath.Parse("bin/x64/plugins/cyber_engine_tweaks/mods/X/init.lua"), "local ns = GetMod(\"nativeSettings\")"),
        ], []);
        Assert.Contains(reqs, r => r.Capability == Capabilities.Codeware);
        Assert.Contains(reqs, r => r.Capability == Capabilities.NativeSettings);
        Assert.Empty(FrameworkCatalog.InferFromContent([(GamePath.Parse("r6/scripts/X/a.reds"), "import Codeware.*")], [Capabilities.Codeware]));
    }

    [Fact]
    public void Audioware_and_virtual_atelier_are_core_mods()
    {
        Assert.Equal([Capabilities.Audioware], FrameworkCatalog.DetectProvided([GamePath.Parse("red4ext/plugins/audioware/audioware.dll"), GamePath.Parse("r6/scripts/Audioware/Ext.reds")]));
        Assert.Equal([Capabilities.VirtualAtelier], FrameworkCatalog.DetectProvided([GamePath.Parse("r6/scripts/virtual-atelier-full/virtual-store/VirtualStoreController.reds")]));
        Assert.Equal(Capabilities.Audioware, FrameworkCatalog.CapabilityOfNexusId("12001"));
        Assert.Equal(Capabilities.VirtualAtelier, FrameworkCatalog.CapabilityOfNexusId("2987"));
        var reqs = FrameworkCatalog.InferFromContent([(GamePath.Parse("r6/scripts/Y/a.reds"), "import Audioware.*")], []);
        Assert.Contains(reqs, r => r.Capability == Capabilities.Audioware);
        var manual = UnmanagedGrouping.Group([("red4ext/plugins/audioware/audioware.dll", 1L), ("r6/scripts/Audioware/Ext.reds", 1L), ("r6/scripts/virtual-atelier-full/SetupNewSite.reds", 1L)]);
        Assert.Contains(manual, d => d.Name == "Audioware" && d.Kind == DetectedKind.Framework);
        Assert.Contains(manual, d => d.Name == "Virtual Atelier" && d.Kind == DetectedKind.Framework);
    }

    [Fact]
    public void Refresh_updates_requirements_of_old_packages()
    {
        using var t = new TestEnv();
        var p = t.ImportZip("X", ("r6/scripts/X/a.reds", "import Codeware.*\nfunc f() {}"));
        // simulate a package stored by an older version without the content rule
        t.C.Store.UpdatePackage(p with { Requirements = p.Requirements.Where(r => r.Capability != Capabilities.Codeware).ToList() });
        Assert.Equal(1, t.C.RefreshRequirements());
        Assert.Contains(t.C.Store.GetPackage(p.PackageId)!.Requirements, r => r.Capability == Capabilities.Codeware);
        Assert.Equal(0, t.C.RefreshRequirements());
    }

    [Fact]
    public void Scripts_of_a_package_are_read_once_per_rules_version()
    {
        using var t = new TestEnv();
        var p = t.ImportZip("X", ("r6/scripts/X/a.reds", "import Codeware.*\nfunc f() {}"));
        Assert.Null(p.RequirementRules);
        Assert.Equal(1, t.C.RefreshRequirements());
        var stored = t.C.Store.GetPackage(p.PackageId)!;
        Assert.Equal(FrameworkCatalog.RulesVersion, stored.RequirementRules);

        // the script is gone from the library: a later pass must not need it and keeps what the first one found
        var script = t.C.Library.PackageFile(p.PackageId, stored.Files.Single().StoredPath);
        File.SetAttributes(script, FileAttributes.Normal);
        File.Delete(script);
        Assert.Equal(0, t.C.RefreshRequirements());
        Assert.Contains(t.C.Store.GetPackage(p.PackageId)!.Requirements, r => r.Capability == Capabilities.Codeware && r.Origin == "content");
    }
}

/// <summary>Requirements listed on the Nexus page of a mod.</summary>
public class NexusRequirementTests
{
    [Fact]
    public void Nexus_page_requirements_count_once_known()
    {
        using var t = new TestEnv();
        var p = t.ImportZip("Y", ("archive/pc/mod/y.archive", "a"));
        t.C.LinkToNexus(p.PackageId, "555");
        NexusInfo.Save(t.C.Store, new NexusModInfo("555", "Y", "short", null, null, 0, 0, null,
            [new NexusRequirement("4198", "ArchiveXL", "https://www.nexusmods.com/cyberpunk2077/mods/4198", false, null)], DateTimeOffset.UtcNow));
        t.C.RefreshRequirements();
        Assert.Contains(t.C.Store.GetPackage(p.PackageId)!.Requirements, r => r.Capability == Capabilities.ArchiveXl && r.Origin == "nexus");
        var row = OverviewBuilder.Build(t.C).Mods.Single();
        Assert.True(row.NexusConfirmed);
        Assert.Equal("https://www.nexusmods.com/cyberpunk2077/mods/555", row.NexusUrl);
        Assert.Empty(row.RequirementLinks); // merged into the requirement list
        var req = Assert.Single(row.Requirements, r => r.Capability == Capabilities.ArchiveXl);
        Assert.Equal("Nexus page", req.Source);
        Assert.Equal("https://www.nexusmods.com/cyberpunk2077/mods/4198", req.NexusUrl);
    }

    [Fact]
    public void Link_to_nexus_needs_a_number()
    {
        using var t = new TestEnv();
        var p = t.ImportZip("Z", ("archive/pc/mod/z.archive", "a"));
        Assert.False(NexusInfo.IdOf(p).Confirmed);
        Assert.Throws<InvalidOperationException>(() => t.C.LinkToNexus(p.PackageId, "abc"));
        var q = t.C.LinkToNexus(p.PackageId, "107");
        Assert.Equal(("107", true), NexusInfo.IdOf(q));
    }

    [Fact]
    public void Parse_graphql_requirements()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""
            {"data":{"mod":{"modRequirements":{"nexusRequirements":{"nodes":[
              {"modId":"107","modName":"Cyber Engine Tweaks","url":"https://www.nexusmods.com/cyberpunk2077/mods/107","externalRequirement":false,"notes":""},
              {"modId":"0","modName":"Some tool","url":"https://example.com","externalRequirement":true,"notes":"needed for X"}]}}}}}
            """);
        var r = NexusClient.ParseRequirements(doc.RootElement);
        Assert.Equal(2, r.Count);
        Assert.Equal("107", r[0].ModId);
        Assert.Null(r[0].Notes);
        Assert.True(r[1].External);
        using var empty = System.Text.Json.JsonDocument.Parse("""{"data":{"mod":null}}""");
        Assert.Empty(NexusClient.ParseRequirements(empty.RootElement));
    }
}

/// <summary>Switching a mod on offers what it needs; switching it off offers what depends on it.</summary>
public class DependencyPromptTests
{
    [Fact]
    public void Switching_on_offers_needed_mods_and_switching_off_offers_dependents()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/version.dll", "v"));
        var mod = t.ImportZip("Lua mod", ("bin/x64/plugins/cyber_engine_tweaks/mods/Lua/init.lua", "x"));
        var o = OverviewBuilder.Build(t.C);
        var need = OverviewBuilder.InactiveRequirements(o, [mod.PackageId]);
        Assert.Equal(cet.PackageId, Assert.Single(need).PackageId);

        t.C.SetEnabled(cet.PackageId, true);
        t.C.SetEnabled(mod.PackageId, true);
        o = OverviewBuilder.Build(t.C);
        Assert.Empty(OverviewBuilder.InactiveRequirements(o, [mod.PackageId]));
        var dependents = OverviewBuilder.ActiveDependents(o, [cet.PackageId]);
        Assert.Equal(mod.PackageId, Assert.Single(dependents).PackageId);
        Assert.Empty(OverviewBuilder.ActiveDependents(o, [mod.PackageId]));
    }

    [Fact]
    public void Missing_requirement_shows_in_the_row()
    {
        using var t = new TestEnv();
        var mod = t.ImportZip("Lua mod", ("bin/x64/plugins/cyber_engine_tweaks/mods/Lua/init.lua", "x"));
        t.C.SetEnabled(mod.PackageId, true);
        var row = OverviewBuilder.Build(t.C).Mods.Single();
        Assert.Contains("Cyber Engine Tweaks", row.Missing);
    }
}

public class LibrarySuggestionTests
{
    [Fact]
    public void Library_goes_next_to_the_exe_when_possible()
    {
        var exe = Path.Combine(Path.GetTempPath(), "afl-suggest", "Tools", "AFTERLIFE");
        var (path, reason) = AppSettings.SuggestLibrary(Path.Combine(Path.GetTempPath(), "afl-suggest", "Games", "Cyberpunk 2077"), exe.Replace("Temp", "Tmp"), _ => true);
        Assert.Null(reason);
        Assert.EndsWith(AppSettings.PortableLibraryFolderName, path);
    }

    [Fact]
    public void Exe_inside_game_folder_or_read_only_falls_back()
    {
        var game = Path.Combine(Path.GetTempPath(), "afl-x", "Cyberpunk 2077");
        Assert.NotNull(AppSettings.SuggestLibrary(game, Path.Combine(game, "tools"), _ => true).FallbackReason);
        Assert.NotNull(AppSettings.SuggestLibrary(null, "/opt/afl", _ => false).FallbackReason);
        // installed with the installer: the library goes to the default place, never into the folder the uninstaller owns
        var installed = AppSettings.SuggestLibrary(null, "/opt/afl", _ => true, _ => true);
        Assert.Equal(AppSettings.DefaultLibraryRoot, installed.Path);
        Assert.Contains("installer", installed.FallbackReason);
        Assert.Null(AppSettings.SuggestLibrary(null, "/opt/afl", _ => true, _ => false).FallbackReason);
    }
}

/// <summary>Switching a mod off: what needs it, what only it needed, and what it needs that stays on.</summary>
public class SwitchOffPromptTests
{
    /// <summary>A plain mod linked to a Nexus page; others can list that page as a requirement.</summary>
    private static PackageManifest Linked(TestEnv t, string name, string nexusId, params string[] requires)
    {
        var p = t.ImportZip(name, ($"archive/pc/mod/{name}.archive", name));
        t.C.LinkToNexus(p.PackageId, nexusId);
        NexusInfo.Save(t.C.Store, new NexusModInfo(nexusId, name, "short", null, null, 0, 0, null,
            requires.Select(r => new NexusRequirement(r, "lib " + r, $"https://www.nexusmods.com/cyberpunk2077/mods/{r}", false, null)).ToList(),
            DateTimeOffset.UtcNow));
        t.C.SetEnabled(p.PackageId, true);
        return p;
    }

    private static InstallOverview Overview(TestEnv t) { t.C.RefreshRequirements(); return OverviewBuilder.Build(t.C); }

    [Fact]
    public void Mod_with_one_dependent()
    {
        using var t = new TestEnv();
        var lib = Linked(t, "Lib", "9001");
        var user = Linked(t, "User", "9002", "9001");
        var o = Overview(t);
        Assert.Equal(user.PackageId, Assert.Single(OverviewBuilder.ActiveDependents(o, [lib.PackageId])).PackageId);
        // switching the user off: the library was needed only by it
        Assert.Equal(lib.PackageId, Assert.Single(OverviewBuilder.UnneededRequirements(o, [user.PackageId])).PackageId);
        Assert.Empty(OverviewBuilder.KeptRequirements(o, [user.PackageId], OverviewBuilder.UnneededRequirements(o, [user.PackageId])));
    }

    [Fact]
    public void Requirement_shared_by_two_mods_stays_on()
    {
        using var t = new TestEnv();
        var lib = Linked(t, "Lib", "9001");
        var a = Linked(t, "A", "9002", "9001");
        Linked(t, "B", "9003", "9001");
        var o = Overview(t);
        var unneeded = OverviewBuilder.UnneededRequirements(o, [a.PackageId]);
        Assert.Empty(unneeded);
        var kept = Assert.Single(OverviewBuilder.KeptRequirements(o, [a.PackageId], unneeded));
        Assert.Equal(lib.PackageId, kept.PackageId);
        Assert.Equal("also needed by B", kept.What);
        Assert.Empty(OverviewBuilder.ActiveDependents(o, [a.PackageId]));
    }

    [Fact]
    public void Chain_of_three_mods()
    {
        using var t = new TestEnv();
        var bottom = Linked(t, "Bottom", "9001");
        var middle = Linked(t, "Middle", "9002", "9001");
        var top = Linked(t, "Top", "9003", "9002");
        var o = Overview(t);
        // the bottom goes off: the middle needs it directly, the top through the middle
        var dependents = OverviewBuilder.ActiveDependents(o, [bottom.PackageId]).Select(d => d.PackageId).ToList();
        Assert.Equal(2, dependents.Count);
        Assert.Contains(middle.PackageId, dependents);
        Assert.Contains(top.PackageId, dependents);
        // the middle goes off: the top depends on it, and the bottom was needed only by it
        Assert.Equal(top.PackageId, Assert.Single(OverviewBuilder.ActiveDependents(o, [middle.PackageId])).PackageId);
        Assert.Equal(bottom.PackageId, Assert.Single(OverviewBuilder.UnneededRequirements(o, [middle.PackageId])).PackageId);
    }

    [Fact]
    public void Core_mods_are_neither_offered_nor_listed()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/version.dll", "v"));
        var mod = t.ImportZip("Lua mod", ("bin/x64/plugins/cyber_engine_tweaks/mods/Lua/init.lua", "x"));
        t.C.SetEnabled(cet.PackageId, true);
        t.C.SetEnabled(mod.PackageId, true);
        var o = Overview(t);
        var unneeded = OverviewBuilder.UnneededRequirements(o, [mod.PackageId]);
        Assert.Empty(unneeded);
        Assert.Empty(OverviewBuilder.KeptRequirements(o, [mod.PackageId], unneeded));
    }
}

/// <summary>The author's note on a Nexus requirement decides whether it is optional.</summary>
public class OptionalNoteTests
{
    [Theory]
    [InlineData("Optional: for the settings menu", true)]
    [InlineData("Recommended", true)]
    [InlineData("v0.9.0+ (optional, only required for JSON)", true)]
    [InlineData("optionally for the CET menu", true)]
    [InlineData("v1.29.0+", false)]
    [InlineData(null, false)]
    public void Optional_is_read_from_the_whole_note(string? note, bool optional) =>
        Assert.Equal(optional, NexusInfo.IsOptionalNote(note));
}
