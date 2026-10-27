using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

public class DeploymentTests
{
    [Fact]
    public void Enable_then_disable_returns_game_to_original_tree()
    {
        using var t = new TestEnv();
        var before = t.Tree();
        var dirsBefore = t.Dirs();
        var a = t.ImportZip("NightCity", ("archive/pc/mod/ncv.archive", "ncv"), ("r6/tweaks/ncv/ncv.yaml", "tweak"));
        t.C.SetEnabled(a.PackageId, true);
        // TweakXL requirement is missing → package excluded until overridden.
        var blocked = t.C.Apply();
        Assert.Contains(blocked.Preview!.Snapshot.Issues, i => i.Kind == IssueKind.MissingRequirement);
        Assert.Null(t.Read("archive/pc/mod/ncv.archive"));

        t.C.UpdateSelected(p => p.WithOverride(a.PackageId, Capabilities.TweakXl));
        t.ApplyOk();
        Assert.Equal("ncv", t.Read("archive/pc/mod/ncv.archive"));
        Assert.Equal("tweak", t.Read("r6/tweaks/ncv/ncv.yaml"));

        t.C.SetEnabled(a.PackageId, false);
        t.ApplyOk();
        Assert.Equal(before, t.Tree());
        Assert.Equal(dirsBefore, t.Dirs());
        Assert.Empty(t.C.Store.GetLedger());
    }

    [Fact]
    public void Second_apply_of_same_profile_is_a_noop()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        var preview = t.C.Preview();
        Assert.True(preview.Plan.IsNoop);
    }

    [Fact]
    public void Disabling_the_winner_restores_the_other_provider()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("r6/scripts/shared.reds", "from A"));
        var b = t.ImportZip("B", ("r6/scripts/shared.reds", "from B"));
        t.C.UpdateSelected(p => p.WithEntry(a.PackageId, true).WithEntry(b.PackageId, true)
            .WithOverride(a.PackageId, Capabilities.Redscript).WithOverride(b.PackageId, Capabilities.Redscript)
            .WithWinner("R6/SCRIPTS/SHARED.REDS", b.PackageId));
        t.ApplyOk();
        Assert.Equal("from B", t.Read("r6/scripts/shared.reds"));
        t.C.SetEnabled(b.PackageId, false);
        t.ApplyOk();
        Assert.Equal("from A", t.Read("r6/scripts/shared.reds"));
    }

    [Fact]
    public void Identical_shared_file_stays_while_another_provider_needs_it()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/shared.archive", "same"), ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("B", ("archive/pc/mod/shared.archive", "same"));
        t.C.UpdateSelected(p => p.WithEntry(a.PackageId, true).WithEntry(b.PackageId, true));
        t.ApplyOk();
        t.C.SetEnabled(a.PackageId, false);
        t.ApplyOk();
        Assert.Equal("same", t.Read("archive/pc/mod/shared.archive"));
        Assert.Null(t.Read("archive/pc/mod/a.archive"));
        Assert.Equal(b.PackageId, t.C.Store.GetLedger()["ARCHIVE/PC/MOD/SHARED.ARCHIVE"].Owner);
    }

    [Fact]
    public void Unresolved_conflict_keeps_incumbent_and_blocks_newcomer()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/x.archive", "A"));
        var b = t.ImportZip("B", ("archive/pc/mod/x.archive", "B"), ("archive/pc/mod/b-only.archive", "b"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        t.C.SetEnabled(b.PackageId, true);
        var r = t.ApplyOk();
        Assert.Contains(r.Preview!.Snapshot.Issues, i => i.Kind == IssueKind.FileConflict && i.PackageId == b.PackageId);
        Assert.Equal("A", t.Read("archive/pc/mod/x.archive"));
        Assert.Null(t.Read("archive/pc/mod/b-only.archive"));
    }

    [Fact]
    public void Foreign_file_at_target_blocks_and_nothing_is_written()
    {
        using var t = new TestEnv();
        t.Write("archive/pc/mod/a.archive", "manual install");
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"), ("archive/pc/mod/a2.archive", "a2"));
        t.C.SetEnabled(a.PackageId, true);
        var r = t.C.Apply();
        Assert.False(r.Succeeded);
        Assert.Contains(r.Preview!.Plan.Blockers, i => i.Kind == IssueKind.ForeignFileAtTarget);
        Assert.Equal("manual install", t.Read("archive/pc/mod/a.archive"));
        Assert.Null(t.Read("archive/pc/mod/a2.archive"));
    }

    [Fact]
    public void Modified_managed_file_requires_decision_and_capture_keeps_change_out_of_game_when_mod_is_off()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "original"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        t.Write("archive/pc/mod/a.archive", "user edit");

        t.C.SetEnabled(a.PackageId, false);
        var blocked = t.C.Apply();
        Assert.Contains(blocked.Preview!.Plan.Blockers, i => i.Kind == IssueKind.ModifiedManagedFile);
        Assert.Equal("user edit", t.Read("archive/pc/mod/a.archive"));

        // Capture the change, re-enable the mod: the captured layer wins; disabling the mod removes the active input.
        t.C.SetEnabled(a.PackageId, true);
        var layer = t.C.CaptureChanges(["archive/pc/mod/a.archive"]);
        t.ApplyOk();
        Assert.Equal("user edit", t.Read("archive/pc/mod/a.archive"));
        Assert.Equal(layer.PackageId, t.C.Store.GetLedger()["ARCHIVE/PC/MOD/A.ARCHIVE"].Owner);

        t.C.SetEnabled(a.PackageId, false);
        t.ApplyOk();
        Assert.Null(t.Read("archive/pc/mod/a.archive"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        Assert.Equal("user edit", t.Read("archive/pc/mod/a.archive"));
    }

    [Fact]
    public void Discard_changes_overwrites_only_with_explicit_decision()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "original"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        t.Write("archive/pc/mod/a.archive", "user edit");
        t.C.SetEnabled(a.PackageId, false);
        var r = t.C.Apply(decisions: new PlanDecisions(new HashSet<string> { "ARCHIVE/PC/MOD/A.ARCHIVE" }));
        Assert.True(r.Succeeded, r.Message);
        Assert.Null(t.Read("archive/pc/mod/a.archive"));
    }

    [Fact]
    public void New_version_that_drops_a_file_removes_it_but_keeps_foreign_files()
    {
        using var t = new TestEnv();
        var v1 = t.ImportZip("Mod-1.0", ("archive/pc/mod/m.archive", "v1"), ("archive/pc/mod/old.archive", "old"));
        t.C.SetEnabled(v1.PackageId, true);
        t.ApplyOk();
        t.Write("archive/pc/mod/foreign.archive", "foreign");
        var v2 = t.C.Import(new ImportRequest(t.Zip("Mod-2.0", ("archive/pc/mod/m.archive", "v2")), "Mod", "2.0", ExistingModId: v1.ModId)).Package!;
        t.C.UpdateSelected(p => p.WithEntry(v1.PackageId, false).WithEntry(v2.PackageId, true));
        t.ApplyOk();
        Assert.Equal("v2", t.Read("archive/pc/mod/m.archive"));
        Assert.Null(t.Read("archive/pc/mod/old.archive"));
        Assert.Equal("foreign", t.Read("archive/pc/mod/foreign.archive"));
    }

    [Fact]
    public void Mods_off_removes_managed_payload_and_keeps_passive_runtime_files()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("bin/x64/plugins/cyber_engine_tweaks/mods/m/init.lua", "lua"));
        t.C.UpdateSelected(p => p.WithEntry(a.PackageId, true).WithOverride(a.PackageId, Capabilities.Cet));
        t.ApplyOk();
        t.Write("bin/x64/plugins/cyber_engine_tweaks/mods/m/settings.json", "{}"); // created by the mod at runtime
        t.C.SelectProfile(ProfileState.ModsOffId);
        t.ApplyOk();
        Assert.Null(t.Read("bin/x64/plugins/cyber_engine_tweaks/mods/m/init.lua"));
        Assert.Equal("{}", t.Read("bin/x64/plugins/cyber_engine_tweaks/mods/m/settings.json"));
        Assert.Empty(t.C.Store.GetLedger());
    }

    [Fact]
    public void Mods_off_profile_cannot_be_edited()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SelectProfile(ProfileState.ModsOffId);
        Assert.Throws<InvalidOperationException>(() => t.C.SetEnabled(a.PackageId, true));
    }

    [Fact]
    public void Running_game_blocks_any_change()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.Env.Running = true;
        var r = t.C.Apply();
        Assert.False(r.Succeeded);
        Assert.Null(t.Read("archive/pc/mod/a.archive"));
    }

    [Fact]
    public void Target_created_between_plan_and_add_is_never_overwritten()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.Fs.BeforeMutation = (op, path) =>
        {
            if (op == "move" && path.EndsWith("a.archive", StringComparison.Ordinal)) File.WriteAllText(path, "sneaky");
        };
        var r = t.C.Apply();
        Assert.False(r.Succeeded);
        Assert.Equal("sneaky", t.Read("archive/pc/mod/a.archive"));
        t.Restart();
        var (outcome, follow) = t.C.Recover(RecoveryChoice.ReturnToPreviousGeneration);
        Assert.Equal("sneaky", t.Read("archive/pc/mod/a.archive"));
        Assert.DoesNotContain("ARCHIVE/PC/MOD/A.ARCHIVE", t.C.Store.GetLedger().Keys);
        Assert.Empty(Directory.EnumerateFiles(t.Game, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Replace_rechecks_full_hash_even_when_size_and_time_match()
    {
        using var t = new TestEnv();
        var v1 = t.ImportZip("M1", ("archive/pc/mod/m.archive", "AAAA"));
        var v2 = t.ImportZip("M2", ("archive/pc/mod/m.archive", "CCCC"));
        t.C.SetEnabled(v1.PackageId, true);
        t.ApplyOk();
        var path = t.GameFile("archive/pc/mod/m.archive");
        var mtime = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, "BBBB"); // same length
        File.SetLastWriteTimeUtc(path, mtime); // same timestamp → passes the fast check
        t.C.UpdateSelected(p => p.WithEntry(v1.PackageId, false).WithEntry(v2.PackageId, true));
        var r = t.C.Apply();
        Assert.False(r.Succeeded);
        Assert.Equal("BBBB", File.ReadAllText(path));
    }

    [Fact]
    public void Writing_through_a_symlinked_directory_is_refused()
    {
        if (OperatingSystem.IsWindows()) return; // symlink creation needs privileges on Windows; covered by M0 junction test
        using var t = new TestEnv();
        var outside = Path.Combine(t.Root, "outside");
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(t.GameFile("archive/pc"));
        Directory.CreateSymbolicLink(t.GameFile("archive/pc/mod"), outside);
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        var r = t.C.Apply();
        Assert.False(r.Succeeded);
        Assert.Empty(Directory.EnumerateFiles(outside));
    }

    [Fact]
    public void Build_change_is_a_warning_not_an_automatic_disable()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        File.WriteAllText(t.GameFile("bin/x64/Cyberpunk2077.exe"), "patched exe 2.6");
        Assert.True(t.C.BuildChanged(out _));
        Assert.Equal("a", t.Read("archive/pc/mod/a.archive"));
        Assert.Throws<InvalidOperationException>(() => t.C.LaunchGame());
        Assert.Contains(t.C.EnvironmentIssues(), i => i.Kind == IssueKind.BuildChanged && i.Severity == Severity.Warning);
    }

    [Fact]
    public void Install_lock_is_exclusive()
    {
        using var t = new TestEnv();
        using var l = InstallLock.Acquire(t.C.Library, t.Game);
        Assert.Throws<InstallBusyException>(() => t.C.Apply());
    }
}

public class OverviewTests
{
    [Fact]
    public void Row_states_follow_intent_versus_disk()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("B", ("archive/pc/mod/b.archive", "b"), ("archive/pc/mod/b.xl", "xl"));
        var off = t.ImportZip("Off", ("archive/pc/mod/off.archive", "o"));
        t.C.UpdateSelected(p => p.WithEntry(a.PackageId, true).WithEntry(b.PackageId, true));

        var o = OverviewBuilder.Build(t.C);
        Assert.Equal(ModState.PendingAdd, o.Mods.Single(m => m.Package.PackageId == a.PackageId).State);
        var bRow = o.Mods.Single(m => m.Package.PackageId == b.PackageId);
        Assert.Equal(ModState.MissingRequirement, bRow.State);
        Assert.Contains(bRow.Requirements, r => r.Capability == Capabilities.ArchiveXl && r.Tone == Tone.Block && r.CanOverride);
        Assert.Equal(ModState.Off, o.Mods.Single(m => m.Package.PackageId == off.PackageId).State);
        Assert.False(o.CanLaunch);

        t.ApplyOk();
        o = OverviewBuilder.Build(t.C);
        Assert.Equal(ModState.Deployed, o.Mods.Single(m => m.Package.PackageId == a.PackageId).State);
        Assert.True(o.IsDeployedProfile);
        Assert.Equal("Game matches this profile", o.ChangesSummary);

        t.C.SetEnabled(a.PackageId, false);
        o = OverviewBuilder.Build(t.C);
        Assert.Equal(ModState.PendingRemoval, o.Mods.Single(m => m.Package.PackageId == a.PackageId).State);
        Assert.Equal(1, o.PendingChanges);
    }

    [Fact]
    public void Conflict_and_outside_change_are_reported_on_the_row()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/x.archive", "A"));
        var b = t.ImportZip("B", ("archive/pc/mod/x.archive", "B"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        t.C.SetEnabled(b.PackageId, true);
        var o = OverviewBuilder.Build(t.C);
        var bRow = o.Mods.Single(m => m.Package.PackageId == b.PackageId);
        Assert.Equal(ModState.Conflict, bRow.State);
        Assert.Contains(bRow.Conflicts, c => c.Path == "archive/pc/mod/x.archive" && c.WinnerName == "A");

        t.Write("archive/pc/mod/x.archive", "edited");
        t.C.UpdateSelected(p => p.WithWinner("ARCHIVE/PC/MOD/X.ARCHIVE", b.PackageId));
        o = OverviewBuilder.Build(t.C);
        Assert.Equal(ModState.ChangedOutside, o.Mods.Single(m => m.Package.PackageId == a.PackageId).State);
    }

    [Fact]
    public void Remove_from_library_requires_the_mod_to_be_off_everywhere()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        Assert.Throws<InvalidOperationException>(() => t.C.RemoveFromLibrary(a.PackageId));
        t.C.SetEnabled(a.PackageId, false);
        t.C.RemoveFromLibrary(a.PackageId);
        Assert.Null(t.C.Store.GetPackage(a.PackageId));
        Assert.DoesNotContain(t.C.SelectedProfile.Entries, e => e.PackageId == a.PackageId);
    }
}

public class CaptureTests
{
    [Fact]
    public void Capture_while_mods_off_is_selected_keeps_edits_with_the_deployed_profile()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "original"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        t.Write("archive/pc/mod/a.archive", "user edit");
        t.C.SelectProfile(ProfileState.ModsOffId);
        Assert.False(t.C.Apply().Succeeded);

        t.C.CaptureChanges(["archive/pc/mod/a.archive"]);
        t.ApplyOk();
        Assert.Null(t.Read("archive/pc/mod/a.archive"));

        t.C.SelectProfile("street");
        t.ApplyOk();
        Assert.Equal("user edit", t.Read("archive/pc/mod/a.archive"));
    }
}
