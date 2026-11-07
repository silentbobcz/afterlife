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
        Assert.True(cet.Removable);
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
        var r = t.C.RemoveUnmanaged(["bin/x64/plugins/cyber_engine_tweaks.asi", "bin/x64/version.dll", "bin/x64/global.ini"], "CET");
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

/// <summary>Files in the game that AFTERLIFE did not write: identical copies are adopted, different ones block and can be moved aside.</summary>
public class ForeignFileTests
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
    public void Identical_leftovers_are_not_in_the_way()
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
}

/// <summary>Files a deployed mod writes by itself (settings, logs, caches, runtime files of a framework) are not manual mods.</summary>
public class RuntimeFileTests
{
    private static (PackageManifest Red4ext, PackageManifest InputLoader) ImportInputLoader(TestEnv t)
    {
        var fw = t.C.Import(new ImportRequest(t.Zip("RED4ext", ("red4ext/RED4ext.dll", "dll")), "RED4ext", "1.31",
            Options: new ImportOptions(AllowUnverifiedFrameworks: true))).Package!;
        var il = t.C.Import(new ImportRequest(t.Zip("InputLoader",
            ("red4ext/plugins/input_loader/input_loader.dll", "il"),
            ("r6/cache/inputContexts.xml", "<vanilla contexts/>"),
            ("r6/cache/inputUserMappings.xml", "<vanilla mappings/>"),
            ("engine/config/platform/pc/input_loader.ini", "[InputLoader]\n")), "Input Loader", "0.2.6",
            Options: new ImportOptions(AllowUnverifiedFrameworks: true))).Package!;
        return (fw, il);
    }

    [Fact]
    public void Catalog_marks_input_loader_runtime_files_as_volatile()
    {
        Assert.True(FrameworkCatalog.IsVolatile("r6/cache/inputContexts.xml"));
        Assert.True(FrameworkCatalog.IsVolatile("R6/Cache/InputUserMappings.xml"));
        Assert.True(FrameworkCatalog.IsVolatile("engine/config/platform/pc/input_loader.ini"));
        Assert.False(FrameworkCatalog.IsVolatile("red4ext/plugins/input_loader/input_loader.dll"));
        Assert.False(FrameworkCatalog.IsVolatile("archive/pc/mod/x.archive"));
    }

    [Fact]
    public void Runtime_files_rewritten_by_the_game_do_not_count_as_changed_outside()
    {
        using var t = new TestEnv();
        var (fw, il) = ImportInputLoader(t);
        t.C.UpdateSelected(p => p.WithEntry(fw.PackageId, true).WithEntry(il.PackageId, true));
        t.ApplyOk();
        Assert.Equal("<vanilla contexts/>", t.Read("r6/cache/inputContexts.xml"));

        // the game ran: Input Loader merged r6/input/*.xml into its cache files and rewrote its ini
        t.Write("r6/cache/inputContexts.xml", "<merged contexts/>");
        t.Write("r6/cache/inputUserMappings.xml", "<merged mappings/>");
        t.Write("engine/config/platform/pc/input_loader.ini", "[InputLoader]\nlast=1\n");

        var preview = t.C.Preview();
        Assert.True(preview.CanApply, string.Join("; ", preview.Plan.Blockers.Select(b => b.Message)));
        Assert.DoesNotContain(preview.Plan.Blockers, b => b.Kind == IssueKind.ModifiedManagedFile);
        Assert.True(preview.Plan.IsNoop); // nothing to do: the merged content is left alone
        var o = OverviewBuilder.Build(t.C);
        Assert.NotEqual(ModState.ChangedOutside, o.Mods.Single(m => m.Package.PackageId == il.PackageId).State);

        // a plain managed file edited by hand still blocks, as before
        t.Write("red4ext/plugins/input_loader/input_loader.dll", "patched");
        preview = t.C.Preview();
        Assert.Contains(preview.Plan.Blockers, b => b.Kind == IssueKind.ModifiedManagedFile && b.Path == "red4ext/plugins/input_loader/input_loader.dll");
    }

    [Fact]
    public void Switching_input_loader_off_removes_its_rewritten_runtime_files_without_a_decision()
    {
        using var t = new TestEnv();
        var (fw, il) = ImportInputLoader(t);
        t.C.UpdateSelected(p => p.WithEntry(fw.PackageId, true).WithEntry(il.PackageId, true));
        t.ApplyOk();
        t.Write("r6/cache/inputContexts.xml", "<merged contexts/>");
        t.Write("engine/config/platform/pc/input_loader.ini", "[InputLoader]\nlast=1\n");

        t.C.SetEnabled(il.PackageId, false);
        var r = t.ApplyOk();
        Assert.True(r.Succeeded, r.Message);
        Assert.Null(t.Read("r6/cache/inputContexts.xml"));
        Assert.Null(t.Read("r6/cache/inputUserMappings.xml"));
        Assert.Null(t.Read("engine/config/platform/pc/input_loader.ini"));
        Assert.Null(t.Read("red4ext/plugins/input_loader/input_loader.dll"));
    }

    [Fact]
    public void Update_of_the_framework_replaces_rewritten_runtime_files_with_its_new_copies()
    {
        using var t = new TestEnv();
        var (fw, il) = ImportInputLoader(t);
        t.C.UpdateSelected(p => p.WithEntry(fw.PackageId, true).WithEntry(il.PackageId, true));
        t.ApplyOk();
        t.Write("r6/cache/inputContexts.xml", "<merged contexts/>");

        var il2 = t.C.Import(new ImportRequest(t.Zip("InputLoader2",
            ("red4ext/plugins/input_loader/input_loader.dll", "il2"),
            ("r6/cache/inputContexts.xml", "<vanilla contexts v2/>"),
            ("r6/cache/inputUserMappings.xml", "<vanilla mappings/>"),
            ("engine/config/platform/pc/input_loader.ini", "[InputLoader]\n")), "Input Loader", "0.2.7", ExistingModId: il.ModId,
            Options: new ImportOptions(AllowUnverifiedFrameworks: true))).Package!;
        t.C.UpdateSelected(p => p.WithEntry(il.PackageId, false).WithEntry(il2.PackageId, true));
        var preview = t.C.Preview();
        Assert.True(preview.CanApply, string.Join("; ", preview.Plan.Blockers.Select(b => b.Message)));
        Assert.Contains(preview.Plan.Steps, s => s.Kind == StepKind.Replace && s.Path == "r6/cache/inputContexts.xml");
        t.ApplyOk();
        Assert.Equal("<vanilla contexts v2/>", t.Read("r6/cache/inputContexts.xml"));
        Assert.Equal("il2", t.Read("red4ext/plugins/input_loader/input_loader.dll"));
    }

    [Fact]
    public void Runtime_file_left_by_a_manual_install_is_taken_over_instead_of_blocking()
    {
        using var t = new TestEnv();
        var (fw, il) = ImportInputLoader(t);
        t.Write("r6/cache/inputContexts.xml", "<merged by an earlier manual install/>");
        t.C.UpdateSelected(p => p.WithEntry(fw.PackageId, true).WithEntry(il.PackageId, true));
        var preview = t.C.Preview();
        Assert.DoesNotContain(preview.Plan.Blockers, b => b.Kind == IssueKind.ForeignFileAtTarget && b.Path == "r6/cache/inputContexts.xml");
        t.ApplyOk();
        Assert.Equal("<vanilla contexts/>", t.Read("r6/cache/inputContexts.xml"));
    }

    [Fact]
    public void Logs_of_mods_deployed_by_afterlife_are_not_manual_mods_and_leave_with_them()
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
}

public class ManualModsNoiseTests
{
    [Fact]
    public void Red4ext_config_and_a_lock_file_of_a_library_mod_are_not_manual_mods()
    {
        using var t = new TestEnv();
        var fw = t.C.Import(new ImportRequest(t.Zip("RED4ext", ("red4ext/RED4ext.dll", "dll")), "RED4ext", "1.31",
            Options: new ImportOptions(AllowUnverifiedFrameworks: true))).Package!;
        var cet = t.ImportZip("AppearanceCreatorMod", ("bin/x64/plugins/cyber_engine_tweaks/mods/AppearanceCreatorMod/init.lua", "lua"));
        t.C.UpdateSelected(p => p.WithEntry(fw.PackageId, true).WithEntry(cet.PackageId, true));
        t.ApplyOk();
        // the game ran
        t.Write("red4ext/config.ini", "[red4ext]");
        t.Write("bin/x64/plugins/cyber_engine_tweaks/mods/AppearanceCreatorMod/ArchiveScans/pending.lock", "");
        var s = t.C.Scan();
        Assert.DoesNotContain(s.Detected, d => d.Files.Any(f => f.Path == "red4ext/config.ini"));
        Assert.DoesNotContain(s.Detected, d => d.Files.Any(f => f.Path.EndsWith("pending.lock")));

        // switched off: the lock file still belongs to the mod in the library (its own folder)
        t.C.SetEnabled(cet.PackageId, false);
        t.ApplyOk();
        s = t.C.Scan();
        Assert.DoesNotContain(s.Detected, d => d.Files.Any(f => f.Path.EndsWith("pending.lock")));
    }

    [Fact]
    public void A_runtime_only_core_group_says_so()
    {
        var r4 = UnmanagedGrouping.Group([("red4ext/logs/red4ext.log", 10L)]).Single(d => d.Name == "RED4ext");
        Assert.Contains("created itself", r4.Note);
        var real = UnmanagedGrouping.Group([("red4ext/RED4ext.dll", 1L), ("red4ext/config.ini", 1L)]).Single(d => d.Name == "RED4ext");
        Assert.Contains("installed manually", real.Note);
    }

    [Fact]
    public void A_mod_folder_with_only_leftovers_is_not_a_manual_mod()
    {
        var g = UnmanagedGrouping.Group(
        [
            ("bin/x64/plugins/cyber_engine_tweaks/mods/AppearanceCreatorMod/ArchiveScans/pending.lock", 0L),
            ("red4ext/plugins/Gone/Gone.log", 10L),
            ("r6/scripts/Old/settings.json", 5L),
            ("bin/x64/plugins/cyber_engine_tweaks/mods/Real/init.lua", 10L),
            ("bin/x64/plugins/cyber_engine_tweaks/mods/Real/data/db.sqlite3", 10L),
            ("r6/scripts/Script/main.reds", 10L),
            ("engine/config/platform/pc/tweak.ini", 10L),
        ]);
        Assert.DoesNotContain(g, d => d.Name is "AppearanceCreatorMod" or "Gone" or "Old");
        Assert.Contains(g, d => d.Name == "Real" && d.Files.Count == 2);
        Assert.Contains(g, d => d.Name == "Script");
        Assert.Contains(g, d => d.Name == "tweak.ini");

        using var t = new TestEnv();
        t.Write("bin/x64/plugins/cyber_engine_tweaks/mods/AppearanceCreatorMod/ArchiveScans/pending.lock", "");
        var s = t.C.Scan();
        Assert.Empty(s.Detected);
        Assert.Empty(s.Unmanaged);
    }

    [Fact]
    public void Address_library_database_written_by_the_game_is_not_a_manual_mod()
    {
        using var t = new TestEnv();
        t.Write("bin/x64/plugins/address_library/version-844424935945371.bin", "offsets");
        var s = t.C.Scan();
        Assert.DoesNotContain(s.Detected, d => d.Files.Any(f => f.Path.Contains("address_library")));
        Assert.DoesNotContain(s.Unmanaged, u => u.Path.Contains("address_library"));
    }
}

/// <summary>Loose files of a core mod that is gone can be cleaned up without touching the mods that used it.</summary>
public class LeftoverTests
{
    [Fact]
    public void Cet_leftovers_move_out_but_cet_mods_stay()
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
