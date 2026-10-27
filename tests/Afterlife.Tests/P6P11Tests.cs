using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

public class GroupingTests
{
    // Mirrors the user's real Inbox (screenshot 2026-10-26).
    private static readonly (string, long)[] Real =
    [
        ("archive/pc/mod/bike_fix.archive", 320_000),
        ("archive/pc/mod/EquipmentEx.archive", 60_000),
        ("archive/pc/mod/EquipmentEx.archive.xl", 666),
        ("archive/pc/mod/ImmersiveRippersDogtown.archive", 108_000),
        ("archive/pc/mod/ImmersiveRippersDogtown.archive.xl", 1_000),
        ("archive/pc/mod/NightCityInteractions.archive", 27_900_000),
        ("archive/pc/mod/NightCityInteractions.archive.xl", 17_000),
        ("r6/scripts/NightCityInteractions/main.reds", 5_000),
        ("r6/scripts/EquipmentEx/Module.reds", 4_000),
        ("r6/tweaks/EquipmentEx/x.yaml", 300),
        ("bin/x64/plugins/cyber_engine_tweaks/mods/VirtualCarDealer/init.lua", 900),
        ("bin/x64/plugins/cyber_engine_tweaks.asi", 3_000_000),
        ("bin/x64/plugins/cyber_engine_tweaks/scripts/json/json.lua", 2_000),
        ("red4ext/RED4ext.dll", 900_000),
        ("red4ext/logs/red4ext.log", 1_000),
        ("red4ext/plugins/ArchiveXL/ArchiveXL.dll", 1_000_000),
        ("red4ext/plugins/TweakXL/TweakXL.dll", 1_000_000),
        ("r6/scripts/TweakXL/TweakXL.reds", 1_000),
        ("red4ext/plugins/MyPlugin/MyPlugin.dll", 100),
        ("mods/SomeRedmod/info.json", 50),
        ("mods/SomeRedmod/archives/x.archive", 50),
    ];

    [Fact]
    public void Archive_xl_and_scripts_with_the_same_name_form_one_mod()
    {
        var g = UnmanagedGrouping.Group(Real);
        var nci = g.Single(x => x.Name == "NightCityInteractions");
        Assert.Equal(3, nci.Files.Count);
        Assert.True(nci.Removable);
        var eq = g.Single(x => x.Name == "EquipmentEx");
        Assert.Equal(4, eq.Files.Count);
        Assert.Single(g.Single(x => x.Name == "bike_fix").Files);
    }

    [Fact]
    public void Frameworks_are_grouped_as_removable_core_mods()
    {
        var g = UnmanagedGrouping.Group(Real);
        var cet = g.Single(x => x.Name == "Cyber Engine Tweaks");
        Assert.Equal(2, cet.Files.Count);
        Assert.True(cet.Removable); // P15
        Assert.True(cet.IsFramework);
        Assert.Equal(Capabilities.Cet, cet.Capability);
        Assert.Equal(Capabilities.Red4ext, g.Single(x => x.Name == "RED4ext").Capability);
        Assert.Equal(2, g.Single(x => x.Name == "TweakXL").Files.Count);
        Assert.True(g.Single(x => x.Name == "MyPlugin").Removable);
        Assert.Equal(DetectedKind.CetMod, g.Single(x => x.Name == "VirtualCarDealer").Kind);
        Assert.Equal(DetectedKind.Redmod, g.Single(x => x.Name == "SomeRedmod").Kind);
        Assert.True(g.Last().Kind == DetectedKind.Framework);
    }

    [Theory]
    [InlineData("X.archive", "X")]
    [InlineData("X.archive.xl", "X")]
    [InlineData("X.xl", "X")]
    [InlineData("#Mod v2.archive", "#Mod v2")]
    public void Archive_stem(string file, string stem) => Assert.Equal(stem, UnmanagedGrouping.ArchiveStem(file));
}

public class RemoveUnmanagedTests
{
    [Fact]
    public void Remove_moves_files_out_and_restore_brings_them_back()
    {
        using var t = new TestEnv();
        t.Write("archive/pc/mod/Old.archive", "old");
        t.Write("archive/pc/mod/Old.archive.xl", "xl");
        t.Write("r6/scripts/Old/deep/a.reds", "script");
        var before = t.Tree();
        var group = t.C.Scan().Detected.Single(d => d.Name == "Old");
        var r = t.C.RemoveUnmanaged(group.Files.Select(f => f.Path).ToList(), group.Name);
        Assert.Equal(3, r.Moved);
        Assert.Null(t.Read("archive/pc/mod/Old.archive"));
        Assert.False(Directory.Exists(t.GameFile("r6/scripts/Old")));
        Assert.True(Directory.Exists(t.GameFile("r6/scripts")));
        Assert.True(Directory.Exists(t.GameFile("archive/pc/mod")));
        Assert.Empty(t.C.Scan().Unmanaged);

        var batch = Assert.Single(t.C.Removed.List());
        Assert.Equal("Old", batch.Label);
        var back = t.C.RestoreRemoved(batch.Id);
        Assert.Equal(3, back.Moved);
        Assert.Equal(before, t.Tree());
        Assert.Empty(t.C.Removed.List());
    }

    [Fact]
    public void Restore_never_overwrites_a_new_file()
    {
        using var t = new TestEnv();
        t.Write("archive/pc/mod/Old.archive", "old");
        t.C.RemoveUnmanaged(["archive/pc/mod/Old.archive"], "Old");
        t.Write("archive/pc/mod/Old.archive", "new one");
        var r = t.C.RestoreRemoved(t.C.Removed.List().Single().Id);
        Assert.Contains("archive/pc/mod/Old.archive", r.Skipped);
        Assert.Equal("new one", t.Read("archive/pc/mod/Old.archive"));
        Assert.Single(t.C.Removed.List());
    }

    [Fact]
    public void Managed_files_outside_paths_and_running_game_are_refused()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        Assert.Throws<InvalidOperationException>(() => t.C.RemoveUnmanaged(["archive/pc/mod/a.archive"], "A"));
        t.Write("bin/x64/plugins/cyber_engine_tweaks.asi", "cet");
        t.Write("bin/x64/version.dll", "loader");
        t.Write("bin/x64/global.ini", "ini");
        var r = t.C.RemoveUnmanaged(["bin/x64/plugins/cyber_engine_tweaks.asi", "bin/x64/version.dll", "bin/x64/global.ini"], "CET"); // P15
        Assert.Equal(3, r.Moved);
        Assert.False(File.Exists(Path.Combine(t.Game, "bin", "x64", "version.dll")));
        t.Write("bin/x64/winmm.dll", "not red4ext"); // only part of RED4ext when RED4ext.dll is there
        Assert.Throws<InvalidOperationException>(() => t.C.RemoveUnmanaged(["bin/x64/winmm.dll"], "winmm"));
        Assert.Throws<InvalidOperationException>(() => t.C.RemoveUnmanaged(["bin/x64/Cyberpunk2077.exe"], "exe"));
        t.Write("archive/pc/mod/x.archive", "x");
        t.Env.Running = true;
        Assert.Throws<InvalidOperationException>(() => t.C.RemoveUnmanaged(["archive/pc/mod/x.archive"], "x"));
        Assert.Equal("x", t.Read("archive/pc/mod/x.archive"));
    }
}

public class GameFolderTests
{
    private static string MakeGame(TestEnv t, string name)
    {
        var g = Path.Combine(t.Root, name);
        Directory.CreateDirectory(Path.Combine(g, "bin", "x64"));
        File.WriteAllText(Path.Combine(g, "bin", "x64", "Cyberpunk2077.exe"), "other exe");
        return g;
    }

    [Fact]
    public void Folder_can_change_when_nothing_is_deployed()
    {
        using var t = new TestEnv();
        var other = MakeGame(t, "Other Game");
        t.C.ChangeGameRoot(other);
        Assert.Equal(Path.GetFullPath(other), t.C.GameRoot);
        Assert.False(t.C.BuildChanged(out _));
        t.Restart();
        Assert.Equal(Path.GetFullPath(other), t.C.GameRoot);
    }

    [Fact]
    public void Folder_change_with_deployed_mods_needs_mods_off_unless_the_install_moved()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        var other = MakeGame(t, "Other Game");
        Assert.Throws<InvalidOperationException>(() => t.C.ChangeGameRoot(other));

        // Same install moved to another drive: every managed file is there, unchanged.
        var moved = Path.Combine(t.Root, "Moved Game");
        CopyDir(t.Game, moved);
        t.C.ChangeGameRoot(moved);
        Assert.True(t.C.Preview().Plan.IsNoop);
    }

    [Fact]
    public void Folder_must_contain_the_game()
    {
        using var t = new TestEnv();
        var empty = Path.Combine(t.Root, "empty");
        Directory.CreateDirectory(empty);
        Assert.Throws<InvalidOperationException>(() => t.C.ChangeGameRoot(empty));
    }

    private static void CopyDir(string from, string to)
    {
        foreach (var d in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(d.Replace(from, to));
        Directory.CreateDirectory(to);
        foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(f, f.Replace(from, to));
            File.SetLastWriteTimeUtc(f.Replace(from, to), File.GetLastWriteTimeUtc(f));
        }
    }
}

public class BulkTests
{
    [Fact]
    public void Enable_many_in_another_profile_and_delete_reports_what_must_stay()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("B", ("archive/pc/mod/b.archive", "b"));
        var photo = t.C.CreateProfile("Photo");
        t.C.SetEnabledMany(photo.Id, [a.PackageId, b.PackageId], true);
        Assert.True(t.C.Store.GetProfile(photo.Id)!.IsEnabled(b.PackageId));
        Assert.False(t.C.SelectedProfile.IsEnabled(a.PackageId));
        Assert.Throws<InvalidOperationException>(() => t.C.SetEnabledMany(ProfileState.ModsOffId, [a.PackageId], true));

        t.C.SelectProfile(photo.Id);
        t.ApplyOk();
        var (deleted, kept) = t.C.DeleteFromLibrary([a.PackageId, b.PackageId]);
        Assert.Empty(deleted);
        Assert.Equal(2, kept.Count);
        Assert.False(t.C.Store.GetProfile(photo.Id)!.IsEnabled(a.PackageId)); // switched off everywhere
        Assert.Equal("a", t.Read("archive/pc/mod/a.archive"));                  // still in game until apply
    }

    [Fact]
    public void Never_deployed_packages_are_deleted_at_once()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("B", ("archive/pc/mod/b.archive", "b"));
        t.C.SetEnabledMany("street", [a.PackageId, b.PackageId], true);
        var (deleted, kept) = t.C.DeleteFromLibrary([a.PackageId, b.PackageId]);
        Assert.Equal(2, deleted.Count);
        Assert.Empty(kept);
        Assert.Empty(t.C.Store.GetPackages());
    }
}

public class P12P26Tests
{
    [Fact]
    public void Delete_everywhere_takes_mods_out_of_the_game_and_the_library()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("B", ("archive/pc/mod/b.archive", "b"));
        var c = t.ImportZip("C", ("archive/pc/mod/c.archive", "c"));
        t.C.SetEnabledMany("street", [a.PackageId, b.PackageId, c.PackageId], true);
        var photo = t.C.CreateProfile("Photo");
        t.C.SetEnabledMany(photo.Id, [a.PackageId], true);
        t.ApplyOk();
        // an older generation used to keep packages ("kept for returning to a recent deployment", P22)
        t.C.SetEnabled(c.PackageId, false);
        t.ApplyOk();
        t.C.SetEnabled(c.PackageId, true); // pending edit that must stay pending

        var r = t.C.DeleteEverywhere([a.PackageId, b.PackageId]);
        Assert.Equal(2, r.Deleted.Count);
        Assert.Empty(r.Kept);
        Assert.NotNull(r.Execution);
        Assert.False(File.Exists(Path.Combine(t.Game, "archive", "pc", "mod", "a.archive")));
        Assert.False(File.Exists(Path.Combine(t.Game, "archive", "pc", "mod", "b.archive")));
        Assert.Single(t.C.Store.GetPackages());
        Assert.DoesNotContain(t.C.Store.GetProfile(photo.Id)!.Entries, e => e.PackageId == a.PackageId);
        Assert.True(t.C.SelectedProfile.IsEnabled(c.PackageId));
        Assert.False(File.Exists(Path.Combine(t.Game, "archive", "pc", "mod", "c.archive"))); // not applied by the delete
    }

    [Fact]
    public void Packages_only_in_older_generations_can_be_deleted()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        t.C.SetEnabled(a.PackageId, false);
        t.ApplyOk();
        var (deleted, kept) = t.C.DeleteFromLibrary([a.PackageId]);
        Assert.Single(deleted);
        Assert.Empty(kept);
    }

    [Fact]
    public void Reorder_sets_entry_order_and_modlist()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("Alpha", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("Beta", ("archive/pc/mod/b.archive", "b"));
        t.C.SetEnabledMany("street", [a.PackageId, b.PackageId], true);
        var p = t.C.Reorder("street", [b.PackageId, a.PackageId]);
        Assert.True(p.ArchiveOrder.Custom);
        Assert.Equal(["b.archive", "a.archive"], p.ArchiveOrder.Order);
        Assert.Equal(b.PackageId, p.Entries[0].PackageId);
        t.ApplyOk();
        var modlist = File.ReadAllLines(Path.Combine(t.Game, "archive", "pc", "mod", "modlist.txt"));
        Assert.True(Array.IndexOf(modlist, "b.archive") < Array.IndexOf(modlist, "a.archive"));
        var o = OverviewBuilder.Build(t.C);
        Assert.True(o.OrderIsCustom);
        Assert.Equal("Beta", o.Mods[0].Package.ModName);
        var q = t.C.ResetOrder("street");
        Assert.False(q.ArchiveOrder.Custom);
        Assert.Throws<InvalidOperationException>(() => t.C.Reorder(ProfileState.ModsOffId, [a.PackageId]));
    }

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

    [Theory]
    [InlineData("nxm://cyberpunk2077/mods/107/files/12345?key=abc&expires=1700000000&user_id=9", true, "107", "12345", "abc")]
    [InlineData("nxm://Cyberpunk2077/mods/4198/files/1", true, "4198", "1", null)]
    [InlineData("nxm://skyrimspecialedition/mods/1/files/2", false, "1", "2", null)]
    public void Nxm_links_are_parsed(string text, bool cyberpunk, string mod, string file, string? key)
    {
        Assert.True(NxmLink.TryParse(text, out var l));
        Assert.Equal(cyberpunk, l.IsCyberpunk);
        Assert.Equal(mod, l.ModId);
        Assert.Equal(file, l.FileId);
        Assert.Equal(key, l.Key);
    }

    [Theory]
    [InlineData("https://www.nexusmods.com/cyberpunk2077/mods/107")]
    [InlineData("nxm://cyberpunk2077/mods/abc/files/1")]
    [InlineData("nxm://cyberpunk2077/collections/xyz")]
    [InlineData("")]
    public void Bad_nxm_links_are_rejected(string text) => Assert.False(NxmLink.TryParse(text, out _));

    [Fact]
    public void Nexus_account_reads_premium_in_both_spellings()
    {
        using var a = System.Text.Json.JsonDocument.Parse("""{"name":"V","is_premium":true,"is_supporter":false}""");
        using var b = System.Text.Json.JsonDocument.Parse("""{"name":"Jackie","is_premium?":false,"is_supporter?":true}""");
        Assert.Equal("Premium", NexusClient.ParseAccount(a.RootElement).Tier);
        Assert.Equal("Supporter", NexusClient.ParseAccount(b.RootElement).Tier);
        using var links = System.Text.Json.JsonDocument.Parse("""[{"name":"CDN","URI":"https://cf-files.nexusmods.com/x.zip"},{"URI":"http://insecure/x"}]""");
        Assert.Single(NexusClient.ParseLinks(links.RootElement));
        Assert.Equal("a_b.zip", NexusClient.SafeFileName("a/b.zip"));
    }
}

public class P27P32Tests
{
    [Fact]
    public void Identical_leftover_file_is_adopted_instead_of_blocking()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/version.dll", "loader"), ("bin/x64/global.ini", "ini"));
        t.Write("bin/x64/version.dll", "loader"); // left over from a manual install, same bytes
        t.C.SetEnabled(cet.PackageId, true);
        var o = OverviewBuilder.Build(t.C);
        Assert.NotEqual(ModState.FileInTheWay, o.Mods.Single().State);
        t.ApplyOk();
        Assert.True(t.C.Store.GetLedger().ContainsKey(GamePath.Parse("bin/x64/version.dll").Key));
    }

    [Fact]
    public void Different_file_outside_mod_folders_is_visible_and_can_be_replaced()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/version.dll", "new loader"), ("bin/x64/global.ini", "ini"));
        t.Write("bin/x64/version.dll", "old loader");
        t.C.SetEnabled(cet.PackageId, true);
        var o = OverviewBuilder.Build(t.C);
        var row = o.Mods.Single();
        Assert.Equal(ModState.FileInTheWay, row.State);
        Assert.Contains("bin/x64/version.dll", row.Explanation);

        var scan = t.C.Scan();
        Assert.Contains(scan.Unmanaged, u => u.Path == "bin/x64/version.dll");
        var g = Assert.Single(scan.Detected, d => d.Blocks.Contains("CET"));
        Assert.Contains("Blocks CET", g.Note);

        Assert.Equal(["bin/x64/version.dll"], t.C.ForeignFilesOf(cet.PackageId));
        var r = t.C.MoveAsideFor(cet.PackageId);
        Assert.Equal(1, r.Moved);
        t.ApplyOk();
        Assert.Equal("new loader", File.ReadAllText(Path.Combine(t.Game, "bin", "x64", "version.dll")));
        // the old file is kept and can be restored once the mod is off again
        Assert.Single(t.C.Removed.List());
    }

    [Fact]
    public void Base_game_files_are_never_moved_aside()
    {
        using var t = new TestEnv();
        Assert.True(InstallCoordinator.IsProtectedGameFile("archive/pc/content/basegame_1_engine.archive"));
        Assert.True(InstallCoordinator.IsProtectedGameFile("bin/x64/Cyberpunk2077.exe"));
        Assert.False(InstallCoordinator.IsProtectedGameFile("bin/x64/version.dll"));
        Assert.Throws<InvalidOperationException>(() => t.C.RemoveUnmanaged(["archive/pc/content/x.archive"], "x"));
    }

    [Fact]
    public void Scan_folders_are_counted_per_bucket()
    {
        var groups = UnmanagedGrouping.WithBlockers(
            UnmanagedGrouping.Group([("r6/config/a.xml", 1), ("archive/pc/mod/x.archive", 1)]),
            new Dictionary<string, IReadOnlyList<string>> { ["r6/config/a.xml"] = ["Input Mod"] });
        Assert.Contains(groups, g => g.Name == "In the way of Input Mod");
    }
}

public class VersionTests
{
    [Theory]
    [InlineData("2.50", "2.5")]
    [InlineData("2.31", "2.31")]
    [InlineData("2.3", "2.3")]
    [InlineData("2.21", "2.21")]
    [InlineData("3.0.88.46235", null)]
    [InlineData("", null)]
    public void Game_patch_from_product_version(string product, string? expected) => Assert.Equal(expected, VersionText.GamePatch(product));

    [Theory]
    [InlineData("1.37.1.0", "1.37.1")]
    [InlineData("v1.31.0+abc", "1.31.0")]
    [InlineData("0.0.0.0", null)]
    [InlineData("1.28", "1.28")]
    public void Clean_framework_version(string raw, string? expected) => Assert.Equal(expected, VersionText.Clean(raw));

    [Fact]
    public void Old_version_text_does_not_raise_a_false_build_warning()
    {
        var old = new GameFingerprint("3.0.5813403", 100, 42);
        var now = new GameFingerprint("3.0.88.46235", 100, 42, "2.5");
        Assert.True(old.SameExecutable(now));
        Assert.Equal("2.5 · build 3.0.88.46235", now.Display);
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
    }
}

public class P33P43Tests
{
    [Fact]
    public void Cet_and_red4ext_archives_import_without_a_guard_P36()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/version.dll", "loader"));
        Assert.Contains(Capabilities.Cet, cet.Provides);
        var r4 = t.ImportZip("RED4ext", ("red4ext/RED4ext.dll", "dll"), ("bin/x64/winmm.dll", "loader"));
        Assert.Contains(Capabilities.Red4ext, r4.Provides);
    }

    [Fact]
    public void Requirements_are_read_from_script_content_P40()
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
    public void Refresh_updates_requirements_of_old_packages_P40()
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
    public void Nexus_page_requirements_count_once_known_P43()
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
        Assert.Empty(row.RequirementLinks); // P46: merged into the requirement list
        var req = Assert.Single(row.Requirements, r => r.Capability == Capabilities.ArchiveXl);
        Assert.Equal("Nexus page", req.Source);
        Assert.Equal("https://www.nexusmods.com/cyberpunk2077/mods/4198", req.NexusUrl);
    }

    [Fact]
    public void Link_to_nexus_needs_a_number_P41()
    {
        using var t = new TestEnv();
        var p = t.ImportZip("Z", ("archive/pc/mod/z.archive", "a"));
        Assert.False(NexusInfo.IdOf(p).Confirmed);
        Assert.Throws<InvalidOperationException>(() => t.C.LinkToNexus(p.PackageId, "abc"));
        var q = t.C.LinkToNexus(p.PackageId, "107");
        Assert.Equal(("107", true), NexusInfo.IdOf(q));
    }

    [Fact]
    public void Parse_graphql_requirements_P43()
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

    [Fact]
    public void Cet_leftovers_move_out_but_cet_mods_stay_P37()
    {
        using var t = new TestEnv();
        t.Write("bin/x64/plugins/cyber_engine_tweaks.asi", "asi");
        t.Write("bin/x64/plugins/cyber_engine_tweaks/cyber_engine_tweaks.log", "log");
        t.Write("bin/x64/plugins/cyber_engine_tweaks/mods/MyMod/init.lua", "mod");
        Assert.Null(t.C.CleanLeftovers(Capabilities.Cet)); // still installed: nothing happens
        File.Delete(t.GameFile("bin/x64/plugins/cyber_engine_tweaks.asi"));
        var r = t.C.CleanLeftovers(Capabilities.Cet);
        Assert.NotNull(r);
        Assert.Null(t.Read("bin/x64/plugins/cyber_engine_tweaks/cyber_engine_tweaks.log"));
        Assert.Equal("mod", t.Read("bin/x64/plugins/cyber_engine_tweaks/mods/MyMod/init.lua"));
        Assert.Contains(t.C.Removed.List(), b => b.Label.Contains("Leftovers of"));
    }
}

public class P44P48Tests
{
    [Fact]
    public void Deployed_files_outside_mod_folders_are_not_in_the_way()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("ArchiveXL", ("red4ext/plugins/ArchiveXL/ArchiveXL.dll", "dll"), ("r6/config/redsUserHints/ArchiveXL.toml", ""), ("r6/scripts/ArchiveXL/a.reds", "x"));
        var r = t.ImportZip("RED4ext", ("red4ext/RED4ext.dll", "dll"), ("bin/x64/winmm.dll", "w"), ("bin/x64/LICENSE", "lic"));
        var c = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/LICENSE", "lic2"));
        foreach (var p in new[] { a, r, c }) t.C.SetEnabled(p.PackageId, true);
        t.ApplyOk();
        Assert.Empty(t.C.Scan().InTheWay);
    }

    [Fact]
    public void Identical_leftovers_are_not_in_the_way_P45()
    {
        using var t = new TestEnv();
        t.Write("r6/config/redsUserHints/EquipmentEx.toml", "hints");
        t.Write("r6/audioware/Drive/a.wav", "OLD");
        var eq = t.ImportZip("Equipment-EX", ("r6/scripts/EquipmentEx/a.reds", "x"), ("r6/config/redsUserHints/EquipmentEx.toml", "hints"));
        var av = t.ImportZip("Drive", ("r6/audioware/Drive/a.wav", "NEW"));
        var s = t.C.Scan();
        Assert.DoesNotContain("r6/config/redsUserHints/EquipmentEx.toml", s.InTheWay.Keys);
        Assert.Contains("r6/audioware/Drive/a.wav", s.InTheWay.Keys);
        Assert.Contains(s.Detected, d => d.Name == "Same as Equipment-EX in the library");
        Assert.Contains(s.Detected, d => d.Name == "In the way of Drive" && d.Note!.Contains("Different version"));

        t.C.SetEnabled(eq.PackageId, true); // enabled: APPLY takes it over, so it is not listed at all
        Assert.DoesNotContain(t.C.Scan().Detected, d => d.Files.Any(f => f.Path.EndsWith("EquipmentEx.toml", StringComparison.OrdinalIgnoreCase)));
        t.ApplyOk();
        Assert.Equal("hints", t.Read("r6/config/redsUserHints/EquipmentEx.toml"));
    }

    [Fact]
    public void Download_history_keeps_newest_first_without_duplicates_P48()
    {
        using var t = new TestEnv();
        DownloadHistory.Record(t.C.Store, new NexusDownload("107", "1", "CET", "1.0", "cet.zip", DateTimeOffset.UtcNow.AddDays(-1)));
        DownloadHistory.Record(t.C.Store, new NexusDownload("4198", "2", "ArchiveXL", "1.2", "axl.zip", DateTimeOffset.UtcNow));
        DownloadHistory.Record(t.C.Store, new NexusDownload("107", "1", "CET", "1.0", "cet.zip", DateTimeOffset.UtcNow));
        var list = DownloadHistory.List(t.C.Store);
        Assert.Equal(["107", "4198"], list.Select(d => d.ModId).ToArray());
        Assert.Equal("https://www.nexusmods.com/cyberpunk2077/mods/107", list[0].Url);
    }
}

public class P53P55Tests
{
    [Fact]
    public void Logs_of_mods_deployed_by_afterlife_are_not_manual_mods_and_leave_with_them_P55()
    {
        using var t = new TestEnv();
        var axl = t.ImportZip("ArchiveXL", ("red4ext/plugins/ArchiveXL/ArchiveXL.dll", "dll"));
        var r4 = t.ImportZip("RED4ext", ("red4ext/RED4ext.dll", "dll"), ("bin/x64/winmm.dll", "w"));
        var pm = t.ImportZip("PhotoModeEx", ("red4ext/plugins/PhotoModeEx/PhotoModeEx.dll", "dll"));
        foreach (var p in new[] { axl, r4, pm }) t.C.SetEnabled(p.PackageId, true);
        t.ApplyOk();
        t.Write("red4ext/plugins/ArchiveXL/ArchiveXL.log", "log");
        t.Write("red4ext/plugins/PhotoModeEx/PhotoModeEx.log", "log");
        t.Write("red4ext/logs/red4ext-2026.log", "log");
        t.Write("red4ext/plugins/Manual/Manual.dll", "manual");      // really manual: still listed
        var s = t.C.Scan();
        Assert.Equal(3, s.RuntimeFiles.Count);
        Assert.DoesNotContain(s.Unmanaged, u => u.Path.EndsWith(".log", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(s.Unmanaged, u => u.Path == "red4ext/plugins/Manual/Manual.dll");

        t.C.SetEnabled(pm.PackageId, false);   // switched off: its files stay (they are its settings) and stay hidden
        Assert.True(t.C.Apply().Succeeded);
        Assert.Equal("log", t.Read("red4ext/plugins/PhotoModeEx/PhotoModeEx.log"));
        Assert.DoesNotContain(t.C.Scan().Unmanaged, u => u.Path.EndsWith("PhotoModeEx.log", StringComparison.OrdinalIgnoreCase));

        var d = t.C.DeleteEverywhere([pm.PackageId]);   // deleted from the library: they leave too, restorable
        Assert.Equal(1, d.Leftovers);
        Assert.Null(t.Read("red4ext/plugins/PhotoModeEx/PhotoModeEx.log"));
        Assert.Equal("log", t.Read("red4ext/plugins/ArchiveXL/ArchiveXL.log"));
        Assert.Contains(t.C.Removed.List(), b => b.Label == "Leftovers of PhotoModeEx");
    }

    [Fact]
    public void Switching_on_offers_needed_mods_and_switching_off_offers_dependents_P53_P54()
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
    public void Missing_requirement_shows_in_the_row_P49()
    {
        using var t = new TestEnv();
        var mod = t.ImportZip("Lua mod", ("bin/x64/plugins/cyber_engine_tweaks/mods/Lua/init.lua", "x"));
        t.C.SetEnabled(mod.PackageId, true);
        var row = OverviewBuilder.Build(t.C).Mods.Single();
        Assert.Contains("Cyber Engine Tweaks", row.Missing);
    }
}

public class P04Tests
{
    [Fact]
    public void Redmods_are_deployed_once_in_load_order_and_again_after_a_change_R2()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("mods/ModA/info.json", "{\"name\":\"ModA\"}"), ("mods/ModA/archives/a.archive", "a"));
        var b = t.ImportZip("B", ("mods/ModB/info.json", "{\"name\":\"ModB\"}"), ("mods/ModB/archives/b.archive", "b"));
        Assert.Equal(RedModOutcome.NoRedMods, t.C.DeployRedMods().Outcome);
        t.C.SetEnabled(b.PackageId, true);
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        Assert.Equal(RedModOutcome.ToolMissing, t.C.DeployRedMods().Outcome);
        t.Write("tools/redmod/bin/redMod.exe", "exe");
        var r = t.C.DeployRedMods();
        Assert.Equal(RedModOutcome.Deployed, r.Outcome);
        Assert.True(r.LaunchModded);
        var run = Assert.Single(t.Env.ToolRuns);
        Assert.Equal("deploy", run.Args[0]);
        Assert.Contains(run.Args, x => x.StartsWith("-modlist=", StringComparison.Ordinal));
        Assert.Equal(RedModOutcome.UpToDate, t.C.DeployRedMods().Outcome);
        t.C.SetEnabled(b.PackageId, false);
        t.ApplyOk();
        Assert.Equal(RedModOutcome.Deployed, t.C.DeployRedMods().Outcome);
        Assert.Equal(["ModA"], t.C.DeployedRedMods());
        t.Env.ToolExitCode = 3;
        var f = t.C.DeployRedMods(force: true);
        Assert.Equal(RedModOutcome.Failed, f.Outcome);
        Assert.False(f.LaunchModded);
    }

    [Fact]
    public void Redmod_archives_import_with_or_without_the_mods_folder_R2()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("mods/ModA/info.json", "{}"), ("mods/ModA/archives/a.archive", "a"), ("mods/ModA/readme.txt", "r"));
        Assert.Equal(["mods/ModA/archives/a.archive", "mods/ModA/info.json"], a.Files.Select(f => f.Target).Order().ToArray());
        var b = t.ImportZip("B", ("ModB/info.json", "{}"), ("ModB/archives/b.archive", "b"));
        Assert.Equal(["mods/ModB/archives/b.archive", "mods/ModB/info.json"], b.Files.Select(f => f.Target).Order().ToArray());
        var c = t.ImportZip("C", ("C v1/mods/ModC/info.json", "{}"), ("C v1/mods/ModC/scripts/x.script", "s"), ("C v1/archive/pc/mod/c.archive", "c"));
        Assert.Contains(c.Files, f => f.Target == "archive/pc/mod/c.archive");
        Assert.Contains(c.Files, f => f.Target == "mods/ModC/scripts/x.script");
    }

    [Fact]
    public void Export_lists_active_mods_with_versions_without_private_paths_P52()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("Blue Moon", ("archive/pc/mod/bm.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        t.ImportZip("Off mod", ("archive/pc/mod/off.archive", "o"));
        var o = OverviewBuilder.Build(t.C);
        var text = ModListExport.Text(o, ExportScope.InGameNow, false, "0.4.0", DateTimeOffset.Now);
        Assert.Contains("Blue Moon", text);
        Assert.Contains("1.0", text);
        Assert.DoesNotContain("Off mod", text);
        Assert.DoesNotContain(t.Game, text);
        Assert.Contains("Off mod", ModListExport.Text(o, ExportScope.InGameNow, true, "0.4.0", DateTimeOffset.Now));
        Assert.StartsWith("order,name,version", ModListExport.Csv(o, ExportScope.InGameNow, false));
    }

    [Fact]
    public void Purge_deletes_kept_files_but_not_mods_P57()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.Write("archive/pc/mod/manual.archive", "m");
        var s = t.C.Scan();
        t.C.RemoveUnmanaged(s.Detected.SelectMany(d => d.Files.Select(f => f.Path)).ToList(), "test");
        Assert.True(t.C.StorageInfo().RemovedBatches > 0);
        File.WriteAllText(Path.Combine(t.C.Library.Downloads, "x.zip"), "zip");
        var freed = t.C.PurgeStorage(true, true, true);
        Assert.True(freed > 0);
        var info = t.C.StorageInfo();
        Assert.Equal(0, info.RemovedBatches);
        Assert.Equal(0, info.Downloads);
        Assert.NotNull(t.C.Store.GetPackage(a.PackageId));
    }
}

public class FomodTests
{
    private const string Xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <config xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <moduleName>Better Lights</moduleName>
          <requiredInstallFiles><folder source="Core" destination="" /></requiredInstallFiles>
          <installSteps order="Explicit">
            <installStep name="Brightness">
              <optionalFileGroups order="Explicit">
                <group name="Level" type="SelectExactlyOne">
                  <plugins order="Explicit">
                    <plugin name="Soft"><description>Softer</description>
                      <files><file source="Soft\lights.archive" destination="archive\pc\mod\lights.archive" /></files>
                      <conditionFlags><flag name="level">soft</flag></conditionFlags>
                      <typeDescriptor><type name="Optional" /></typeDescriptor></plugin>
                    <plugin name="Bright"><description>Brighter</description>
                      <files><file source="Bright\lights.archive" destination="archive\pc\mod\lights.archive" /></files>
                      <conditionFlags><flag name="level">bright</flag></conditionFlags>
                      <typeDescriptor><type name="Recommended" /></typeDescriptor></plugin>
                  </plugins>
                </group>
              </optionalFileGroups>
            </installStep>
            <installStep name="Extras">
              <visible><flagDependency flag="level" value="bright" /></visible>
              <optionalFileGroups order="Explicit">
                <group name="Extras" type="SelectAny">
                  <plugins order="Explicit">
                    <plugin name="Neon"><description>Neon</description>
                      <files><folder source="Neon" destination="" /></files>
                      <typeDescriptor><type name="Optional" /></typeDescriptor></plugin>
                  </plugins>
                </group>
              </optionalFileGroups>
            </installStep>
          </installSteps>
          <conditionalFileInstalls><patterns>
            <pattern><dependencies operator="And"><flagDependency flag="level" value="soft" /></dependencies>
              <files><file source="Soft\soft.xl" destination="archive\pc\mod\soft.xl" /></files></pattern>
          </patterns></conditionalFileInstalls>
        </config>
        """;

    [Fact]
    public void Parses_steps_defaults_visibility_and_conditional_files_R3()
    {
        var cfg = Fomod.Parse(Xml);
        Assert.Equal("Better Lights", cfg.Name);
        Assert.Equal(2, cfg.Steps.Count);
        // default = recommended "Bright" → Extras step visible
        Assert.Equal(2, cfg.VisibleSteps([]).Count);
        var soft = new[] { new FomodChoice("Brightness", "Level", ["Soft"]) };
        Assert.Single(cfg.VisibleSteps(soft));
        var files = cfg.Resolve(soft);
        Assert.Contains(files, f => f.Source == "Soft\\\\soft.xl".Replace("\\\\", "\\"));
        Assert.Contains(files, f => f.IsFolder && f.Source == "Core");
    }

    [Fact]
    public void Imports_the_chosen_options_and_stores_the_choices_R3()
    {
        using var t = new TestEnv();
        var zip = t.Zip("lights",
            ("Better Lights/fomod/ModuleConfig.xml", Xml),
            ("Better Lights/Core/r6/scripts/BL/core.reds", "core"),
            ("Better Lights/Soft/lights.archive", "soft"),
            ("Better Lights/Soft/soft.xl", "xl"),
            ("Better Lights/Bright/lights.archive", "bright"),
            ("Better Lights/Neon/archive/pc/mod/neon.archive", "neon"));
        var first = t.C.Import(new ImportRequest(zip, "Better Lights", "1.0"));
        Assert.Equal(ImportStatus.NeedsFomod, first.Analysis.Status);
        var cfg = t.C.ReadFomod(zip, first.Analysis.FomodConfigPath!);
        var choices = new[] { new FomodChoice("Brightness", "Level", ["Bright"]), new FomodChoice("Extras", "Extras", ["Neon"]) };
        var r = t.C.Import(new ImportRequest(zip, "Better Lights", "1.0", Options: new ImportOptions { FomodFiles = cfg.Resolve(choices) }) { FomodChoices = choices });
        Assert.True(r.Succeeded, string.Join("; ", r.Analysis.Problems));
        Assert.Equal(["archive/pc/mod/lights.archive", "archive/pc/mod/neon.archive", "r6/scripts/BL/core.reds"], r.Package!.Files.Select(f => f.Target).Order().ToArray());
        Assert.Equal("Bright", t.C.Store.GetPackage(r.Package.PackageId)!.FomodChoices![0].Plugins[0]);
    }
}

public class UpdateTests
{
    private static NexusFiles Files() => NexusClient.ParseFiles("107", System.Text.Json.JsonDocument.Parse("""
        {"files":[
          {"file_id":1,"name":"CET","version":"1.30","category_name":"OLD_VERSION","uploaded_timestamp":100,"file_name":"cet130.zip"},
          {"file_id":2,"name":"CET","version":"1.35","category_name":"MAIN","uploaded_timestamp":200,"file_name":"cet135.zip"},
          {"file_id":3,"name":"CET hotfix","version":"1.35.1","category_name":"MAIN","uploaded_timestamp":300,"file_name":"cet1351.zip"}],
         "file_updates":[{"old_file_id":1,"new_file_id":2},{"old_file_id":2,"new_file_id":3}]}
        """).RootElement);

    [Fact]
    public void Follows_replaced_files_to_the_newest_M4()
    {
        var u = NexusUpdates.Find(Files(), "1", "1.30", DateTimeOffset.UtcNow);
        Assert.NotNull(u);
        Assert.Equal("3", u!.FileId);
        Assert.True(u.Exact);
        Assert.Null(NexusUpdates.Find(Files(), "3", "1.35.1", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Without_a_file_id_compares_the_main_version_M4()
    {
        var u = NexusUpdates.Find(Files(), null, "1.30", DateTimeOffset.UtcNow);
        Assert.Equal("1.35.1", u!.Version);
        Assert.False(u.Exact);
        Assert.Null(NexusUpdates.Find(Files(), null, "v1.35.1", DateTimeOffset.UtcNow));
        Assert.NotNull(NexusUpdates.Find(Files(), null, "1.35.10", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Checks_at_most_once_a_day_M4()
    {
        using var t = new TestEnv();
        var now = DateTimeOffset.UtcNow;
        Assert.False(NexusUpdates.IsFresh(t.C.Store, "pkg-x", now));
        NexusUpdates.Save(t.C.Store, "pkg-x", null, now);
        Assert.True(NexusUpdates.IsFresh(t.C.Store, "pkg-x", now.AddHours(5)));
        Assert.False(NexusUpdates.IsFresh(t.C.Store, "pkg-x", now.AddHours(25)));
    }
}

public class CollectionTests
{
    [Theory]
    [InlineData("nxm://cyberpunk2077/collections/rcuccp/revisions/latest", "rcuccp", null)]
    [InlineData("nxm://cyberpunk2077/collections/g14kxi/revisions/2", "g14kxi", 2)]
    [InlineData("https://www.nexusmods.com/games/cyberpunk2077/collections/abc123", "abc123", null)]
    [InlineData("https://next.nexusmods.com/cyberpunk2077/collections/abc123/revisions/7", "abc123", 7)]
    public void Parses_collection_links_P51(string url, string slug, int? rev)
    {
        Assert.True(CollectionLink.TryParse(url, out var l));
        Assert.Equal(slug, l.Slug);
        Assert.Equal(rev, l.Revision);
        Assert.Equal("cyberpunk2077", l.Game);
        Assert.False(CollectionLink.TryParse("nxm://cyberpunk2077/mods/107/files/1", out _));
    }

    internal static CollectionRevisionInfo Api() => CollectionLogic.FromGraphQl("abc", System.Text.Json.JsonDocument.Parse("""
        {"data":{"collectionRevision":{"revisionNumber":3,"downloadLink":"/v1/collections/abc/revisions/3/download_link","installationInfo":"Run the game once.",
          "adultContent":false,"totalSize":1000,
          "collection":{"name":"Night City Plus","summary":"s","slug":"abc","user":{"name":"Curator"},"latestPublishedRevision":{"revisionNumber":4}},
          "modFiles":[
            {"fileId":11,"optional":false,"version":"1.0","updatePolicy":"prefer","file":{"fileId":11,"name":"A main","version":"1.0","modId":101,"sizeInBytes":10,"mod":{"name":"Mod A"}}},
            {"fileId":22,"optional":false,"version":"2.0","updatePolicy":"prefer","file":{"fileId":22,"name":"B main","version":"2.0","modId":202,"sizeInBytes":10,"mod":{"name":"Mod B"}}},
            {"fileId":33,"optional":true,"version":"3.0","updatePolicy":"prefer","file":{"fileId":33,"name":"C","version":"3.0","modId":303,"sizeInBytes":10,"mod":{"name":"Mod C"}}}],
          "externalResources":[{"name":"Guide tool","version":"1","optional":true,"resourceUrl":"https://example.com","instructions":"by hand"}]}}}
        """).RootElement);

    [Fact]
    public void Reads_the_revision_from_the_api_P51()
    {
        var r = Api();
        Assert.Equal("Night City Plus", r.Name);
        Assert.Equal(3, r.Revision);
        Assert.Equal(4, r.LatestRevision);
        Assert.Equal(4, r.Items.Count);
        Assert.True(r.Items.Single(i => i.Name == "Mod C").Optional);
        Assert.Equal(CollectionSource.Browse, r.Items.Single(i => i.Name == "Guide tool").Source);
    }

    [Fact]
    public void Manifest_adds_installer_choices_and_rules_P51()
    {
        var json = """
            {"info":{"name":"Night City Plus"},
             "mods":[
               {"name":"Mod A","version":"1.0","optional":false,"source":{"type":"nexus","modId":101,"fileId":11,"md5":"aaa","logicalFilename":"A main"},
                "choices":{"type":"fomod","options":[{"name":"Main","groups":[{"name":"Color","choices":[{"name":"Red","idx":0}]}]}]}},
               {"name":"Mod B","version":"2.0","optional":false,"source":{"type":"nexus","modId":202,"fileId":22,"md5":"bbb","logicalFilename":"B main"}},
               {"name":"Bundled tweak","version":"1","optional":false,"source":{"type":"bundle","logicalFilename":"tweak"}}],
             "modRules":[{"type":"before","source":{"fileMD5":"bbb"},"reference":{"fileMD5":"aaa"}}]}
            """;
        var r = CollectionLogic.FromManifest(Api(), json);
        Assert.True(r.FromBundle);
        var a = r.Items.Single(i => i.Name == "Mod A");
        Assert.Equal("Red", a.Choices![0].Plugins[0]);
        Assert.Equal(CollectionSource.Bundle, r.Items.Single(i => i.Name == "Bundled tweak").Source);
        var rule = Assert.Single(r.Rules);
        Assert.Equal(("before", "file:22", "file:11"), (rule.Type, rule.From, rule.To));
    }

    [Fact]
    public void Order_follows_the_collection_rules_and_modlist_P51()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("Mod A", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("Mod B", ("archive/pc/mod/b.archive", "b"));
        var api = Api();
        var rev = api with { Rules = [new CollectionRule("before", "file:22", "file:11")] };
        var rec = new CollectionRecord(rev, null, new Dictionary<string, string> { ["file:11"] = a.PackageId, ["file:22"] = b.PackageId }, [], [], true, DateTimeOffset.UtcNow);
        Assert.Equal([b.PackageId, a.PackageId], CollectionLogic.Order(rec, t.C.Store.GetPackages()));
        var withList = rec with { Revision = rev with { Rules = [], ArchiveOrder = ["a.archive", "b.archive"] } };
        Assert.Equal([a.PackageId, b.PackageId], CollectionLogic.Order(withList, t.C.Store.GetPackages()));
    }

    [Fact]
    public void Removal_keeps_what_others_need_P51()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/version.dll", "v"));
        var onlyHere = t.ImportZip("Mod A", ("archive/pc/mod/a.archive", "a"));
        var shared = t.ImportZip("Mod B", ("archive/pc/mod/b.archive", "b"));
        var mine = t.ImportZip("My Lua", ("bin/x64/plugins/cyber_engine_tweaks/mods/Lua/init.lua", "x"));
        foreach (var p in new[] { cet, onlyHere, shared, mine }) t.C.SetEnabled(p.PackageId, true);
        var api = Api();
        var r1 = new CollectionRecord(api, null, new Dictionary<string, string> { ["file:11"] = onlyHere.PackageId, ["file:22"] = shared.PackageId, ["file:99"] = cet.PackageId }, [], [], true, DateTimeOffset.UtcNow);
        var r2 = new CollectionRecord(api with { Slug = "other", Name = "Other" }, null, new Dictionary<string, string> { ["file:22"] = shared.PackageId }, [], [], true, DateTimeOffset.UtcNow);
        var o = OverviewBuilder.Build(t.C);
        var (remove, keep) = CollectionLogic.RemovalPlan(r1, [r1, r2], o);
        Assert.Equal([onlyHere.PackageId], remove.Select(x => x.PackageId).ToArray());
        Assert.Contains(keep, k => k.PackageId == shared.PackageId && k.KeptBecause!.Contains("Other"));
        Assert.Contains(keep, k => k.PackageId == cet.PackageId && k.KeptBecause!.Contains("My Lua"));
    }

    [Fact]
    public void Diff_between_revisions_P51()
    {
        var old = Api();
        var items = old.Items.Where(i => i.Name != "Mod B").Select(i => i.Name == "Mod A" ? i with { FileId = "12", Version = "1.1" } : i).ToList();
        items.Add(new CollectionItem("Mod D", "1", false, CollectionSource.Nexus, "404", "44", null, null, null, null, null, null, 0, null));
        var d = CollectionLogic.Diff(old, old with { Items = items, Revision = 4 });
        Assert.Equal("Mod D", Assert.Single(d.Added).Name);
        Assert.Equal("Mod B", Assert.Single(d.Removed).Name);
        Assert.Equal("1.1", Assert.Single(d.Updated).New.Version);
    }

    [Fact]
    public void Records_are_stored_and_listed_P51()
    {
        using var t = new TestEnv();
        var rec = new CollectionRecord(Api(), "p1", new Dictionary<string, string>(), [], [], true, DateTimeOffset.UtcNow);
        CollectionStore.Save(t.C.Store, rec);
        Assert.Equal("Night City Plus", Assert.Single(t.C.Collections()).Name);
        Assert.Equal(2, rec.Missing.Count); // required A and B; optional C and the external tool not chosen
        CollectionStore.Delete(t.C.Store, "abc");
        Assert.Empty(t.C.Collections());
    }
}

public class SsoTests
{
    [Fact]
    public void Reads_sso_messages_and_stays_hidden_without_a_slug()
    {
        Assert.False(NexusSso.Available);
        Assert.Null(NexusSso.ParseMessage("""{"success":true,"data":{"connection_token":"t"},"error":null}""", out var c));
        Assert.True(c);
        Assert.Equal("KEY", NexusSso.ParseMessage("""{"success":true,"data":{"api_key":"KEY"},"error":null}""", out _));
        Assert.Throws<NexusException>(() => NexusSso.ParseMessage("""{"success":false,"data":null,"error":"bad"}""", out _));
    }
}

public class UpdateFeedTests
{
    [Fact]
    public void Hourly_check_reads_files_only_for_changed_mods()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var hourAgo = now.AddHours(-1);
        var json = "[{\"mod_id\":1,\"latest_file_update\":" + now.AddMinutes(-20).ToUnixTimeSeconds() + ",\"latest_mod_activity\":0},"
                 + "{\"mod_id\":2,\"latest_file_update\":" + now.AddHours(-5).ToUnixTimeSeconds() + ",\"latest_mod_activity\":0}]";
        var feed = NexusClient.ParseUpdated(System.Text.Json.JsonDocument.Parse(json).RootElement);
        Assert.Equal("1d", NexusUpdates.FeedPeriod(hourAgo, now));
        Assert.Equal("1w", NexusUpdates.FeedPeriod(now.AddDays(-2), now));
        Assert.Null(NexusUpdates.FeedPeriod(now.AddDays(40), now.AddDays(80)));
        var due = NexusUpdates.DueAfterFeed([("a", "1", hourAgo), ("b", "2", hourAgo), ("c", "3", hourAgo), ("d", "4", null)], feed, now);
        Assert.Equal(["a", "d"], due);
    }
}

public class P58P64Tests
{
    [Theory]
    [InlineData("Mod Settings v0.2.27 for 2.50-4885-0.2.27-2026-10-26T22-14Z-cB3xB00IB.zip", "Mod Settings v0.2.27 for 2.50", "0.2.27", "4885")]
    [InlineData("Mod Settings v0.2.27 for 2.50 4885 0.2.27 2026-10-26T22-14Z cB3xB00IB.zip", "Mod Settings v0.2.27 for 2.50", "0.2.27", "4885")]
    [InlineData("Native Settings UI-3518-1-97-1700000000.zip", "Native Settings UI", "1.97", "3518")]
    public void Recognizes_nexus_download_names_P63(string file, string name, string version, string id)
    {
        var g = PackageLibrary.GuessFromFileName(file);
        Assert.Equal((name, version, id), (g.Name, g.Version, g.NexusModId));
    }

    [Fact]
    public void Deleting_a_mod_that_changed_its_own_settings_keeps_the_copy_in_removed()
    {
        using var t = new TestEnv();
        var m = t.ImportZip("Truck", ("archive/pc/mod/truck.archive", "x"), ("archive/pc/mod/truck_settings.json", "{}"));
        t.C.SetEnabled(m.PackageId, true);
        t.ApplyOk();
        var settings = Path.Combine(t.Game, "archive", "pc", "mod", "truck_settings.json");
        File.SetAttributes(settings, FileAttributes.Normal);
        File.WriteAllText(settings, "{\"speed\":2}");

        var r = t.C.DeleteEverywhere([m.PackageId]);
        Assert.Single(r.Deleted);
        Assert.Empty(r.Kept);
        Assert.False(File.Exists(settings));
        Assert.Contains(t.C.Removed.List(), b => b.Label.Contains("Changed files of Truck"));
    }
}

public class P66P72Tests
{
    [Fact]
    public void Profiles_can_be_deleted_but_not_the_one_in_the_game_P72()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        var street = t.C.SelectedProfile.Id;
        var photo = t.C.CreateProfile("Photo", street);
        Assert.Throws<InvalidOperationException>(() => t.C.DeleteProfile(street));            // in the game now
        Assert.Throws<InvalidOperationException>(() => t.C.DeleteProfile(ProfileState.ModsOffId));
        t.C.SelectProfile(photo.Id);
        t.C.DeleteProfile(photo.Id);
        Assert.Null(t.C.Store.GetProfile(photo.Id));
        Assert.Equal(street, t.C.SelectedProfile.Id);                                         // back to the deployed one
        Assert.NotNull(t.C.Store.GetPackage(a.PackageId));                                     // mods stay
    }
}

public class P73P86Tests
{
    [Fact]
    public void Mods_can_be_renamed_and_named_back_P76()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("Some mod-1234-1-0-1700000000", ("archive/pc/mod/a.archive", "a"));
        var original = a.ModName;
        Assert.Equal("Truck sim", t.C.RenamePackage(a.PackageId, "  Truck sim ").ModName);
        Assert.Equal("Truck sim", t.C.Store.GetPackage(a.PackageId)!.ModName);
        Assert.Equal("Other", t.C.RenamePackage(a.PackageId, "Other").ModName);
        Assert.Equal(original, t.C.RenamePackage(a.PackageId, "").ModName);   // empty = original name
    }

    [Fact]
    public void Collection_uses_a_core_mod_already_in_the_library_P81()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/version.dll", "v"));
        var withId = cet with { NexusModId = "107", NexusFileId = "1" };
        t.C.Store.UpdatePackage(withId);
        var item = new CollectionItem("Cyber Engine Tweaks", "1.38", false, CollectionSource.Nexus, "107", "999", null, null, null, null, null, null, 0, null);
        var rev = new CollectionRevisionInfo("abc", "Test", null, 1, null, null, null, false, 0, null, [item], []);
        var s = Assert.Single(CollectionLogic.States(rev, t.C.Store.GetPackages()));
        Assert.Equal(ItemStatus.InLibrary, s.Status);
        Assert.Equal(cet.PackageId, s.PackageId);
    }
}
