using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;


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
