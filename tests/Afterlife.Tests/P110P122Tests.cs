using Afterlife.Core;
using Afterlife.Infrastructure;
using Xunit;

namespace Afterlife.Tests;

/// <summary>P115/P119: a framework rewrites its own shipped files while the game runs; that is not a user edit.</summary>
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
}

/// <summary>P114: what the plan says about mods held back because their framework is off.</summary>
public class PlanGroupingTests
{
    [Fact]
    public void Summary_and_groups_name_mods_held_back_by_a_missing_framework()
    {
        using var t = new TestEnv();
        var fw = t.C.Import(new ImportRequest(t.Zip("RED4ext", ("red4ext/RED4ext.dll", "dll")), "RED4ext", "1.31",
            Options: new ImportOptions(AllowUnverifiedFrameworks: true))).Package!;
        var axl = t.C.Import(new ImportRequest(t.Zip("ArchiveXL", ("red4ext/plugins/ArchiveXL/ArchiveXL.dll", "axl")), "ArchiveXL", "1.28",
            Options: new ImportOptions(AllowUnverifiedFrameworks: true))).Package!;
        var mod = t.ImportZip("Visuals", ("archive/pc/mod/v.archive", "v"), ("archive/pc/mod/v.xl", "xl"));
        t.C.UpdateSelected(p => p.WithEntry(fw.PackageId, true).WithEntry(axl.PackageId, true).WithEntry(mod.PackageId, true));
        t.ApplyOk();

        t.C.SetEnabled(axl.PackageId, false);
        var preview = t.C.Preview();
        var summary = OverviewBuilder.Summary(preview.Plan, preview.Snapshot);
        Assert.Contains("1 mod held back", summary);
        Assert.Contains("3 remove", summary); // ArchiveXL.dll + v.archive + v.xl

        var groups = OverviewBuilder.GroupPlan(preview, id => id == mod.PackageId ? "Visuals" : id == axl.PackageId ? "ArchiveXL" : null);
        var visuals = groups.Single(g => g.PackageId == mod.PackageId);
        Assert.Equal(2, visuals.Remove);
        Assert.StartsWith("held back", visuals.Why);
        Assert.Contains("ArchiveXL", visuals.Why);
        var archiveXl = groups.Single(g => g.PackageId == axl.PackageId);
        Assert.Equal("switched off", archiveXl.Why);
        Assert.Equal(1, archiveXl.Remove);
    }
}

/// <summary>P113: the activity log writes plans and switches when enabled and stays silent otherwise.</summary>
public class ActivityLogTests
{
    [Fact]
    public void Log_records_switches_and_plan_steps_only_when_enabled()
    {
        using var t = new TestEnv();
        var dir = Path.Combine(t.Root, "logs");
        ActivityLog.Directory = dir;
        ActivityLog.Enabled = false;
        try
        {
            var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "A"));
            t.C.SetEnabled(a.PackageId, true);
            t.ApplyOk();
            Assert.False(Directory.Exists(dir) && Directory.EnumerateFiles(dir).Any());

            ActivityLog.Enabled = true;
            t.C.SetEnabled(a.PackageId, false);
            t.ApplyOk();
            var text = File.ReadAllText(ActivityLog.CurrentFile);
            Assert.Contains("off A", text);
            Assert.Contains("Remove  archive/pc/mod/a.archive", text);
            Assert.Contains("no longer in profile", text);
            Assert.Contains("Verified after 1 step(s)", text);
        }
        finally
        {
            ActivityLog.Enabled = false;
        }
    }
}

/// <summary>P123: an update takes the old version's place everywhere, so the old one can go.</summary>
public class ReplaceVersionTests
{
    [Fact]
    public void New_version_takes_the_place_of_the_old_one_and_the_old_one_can_be_deleted_after_apply()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/x.archive", "A"));
        var b = t.ImportZip("B", ("archive/pc/mod/x.archive", "B"));
        var old = t.ImportZip("Mod", ("archive/pc/mod/mod.archive", "v1"));
        t.C.UpdateSelected(p => p.WithEntry(a.PackageId, true).WithEntry(b.PackageId, true).WithEntry(old.PackageId, true)
            .WithWinner("ARCHIVE/PC/MOD/X.ARCHIVE", a.PackageId));
        t.ApplyOk();
        var neu = t.C.Import(new ImportRequest(t.Zip("Mod2", ("archive/pc/mod/mod.archive", "v2")), "Mod", "2.0", ExistingModId: old.ModId)).Package!;
        // winner rule pointing at the old version is carried over too
        t.C.UpdateSelected(p => p.WithWinner("ARCHIVE/PC/MOD/X.ARCHIVE", old.PackageId));

        t.C.ReplaceVersion(old.PackageId, neu.PackageId);
        var p = t.C.SelectedProfile;
        Assert.DoesNotContain(p.Entries, e => e.PackageId == old.PackageId);
        Assert.True(p.IsEnabled(neu.PackageId));
        Assert.Equal(neu.PackageId, p.PathWinners["ARCHIVE/PC/MOD/X.ARCHIVE"]);

        t.ApplyOk();
        Assert.Equal("v2", t.Read("archive/pc/mod/mod.archive"));
        var (deleted, kept) = t.C.DeleteFromLibrary([old.PackageId]);
        Assert.Single(deleted);
        Assert.Empty(kept);
        Assert.Null(t.C.Store.GetPackage(old.PackageId));
        Assert.Equal("v2", t.Read("archive/pc/mod/mod.archive"));
    }
}

/// <summary>P125: files mods write while running are not listed as manual mods.</summary>
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
    public void Address_library_database_gets_a_name_and_a_runtime_only_core_group_says_so()
    {
        var g = UnmanagedGrouping.Group([("bin/x64/plugins/address_library/version-844424935945371.bin", 18_000_000L), ("red4ext/logs/red4ext.log", 10L)]);
        var al = g.Single(d => d.Id == "other:addresslibrary");
        Assert.StartsWith("Address Library", al.Name);
        Assert.Contains("Offset database", al.Note);
        var r4 = g.Single(d => d.Name == "RED4ext");
        Assert.Contains("created itself", r4.Note);
        var real = UnmanagedGrouping.Group([("red4ext/RED4ext.dll", 1L), ("red4ext/config.ini", 1L)]).Single(d => d.Name == "RED4ext");
        Assert.Contains("installed manually", real.Note);
    }
}
