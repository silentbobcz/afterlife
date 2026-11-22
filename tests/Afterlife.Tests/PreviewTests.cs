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

[Collection("activity log")] // both classes switch the one static log
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

[Collection("activity log")]
public class LogPrivacyTests
{
    private static string Scrub(string text) => LogPrivacy.Scrub(text, @"D:\Games\Cyberpunk 2077", @"C:\Users\Jan Novak\AppData\Local\AFTERLIFE\Library",
        "Jan Novak", "NOVAK-PC", @"C:\Users\Jan Novak");

    [Theory]
    [InlineData(@"Access to the path 'D:\Games\Cyberpunk 2077\archive\pc\mod\a.archive' is denied.", @"Access to the path '<game>\archive\pc\mod\a.archive' is denied.")]
    [InlineData(@"could not read C:\Users\Jan Novak\AppData\Local\AFTERLIFE\Library\packages\pkg-1\x", @"could not read <library>\packages\pkg-1\x")]
    [InlineData(@"C:/Users/Jan Novak/Downloads/Mod-107-1-0-1700000000.zip", @"<home>/Downloads/Mod-107-1-0-1700000000.zip")]
    [InlineData(@"E:\Users\Petra\Desktop\mod.zip not found", @"E:\Users\<user>\Desktop\mod.zip not found")]
    [InlineData(@"\\NAS-DOMA\mods\a.zip", @"\\<server>\mods\a.zip")]
    [InlineData("nxm://cyberpunk2077/mods/107/files/5?key=AbC123&expires=1700000000&user_id=42", "nxm://cyberpunk2077/mods/107/files/5?key=<hidden>&expires=<hidden>&user_id=<hidden>")]
    [InlineData("owner Jan Novak on NOVAK-PC, mail jan.novak@example.com", "owner <user> on <pc>, mail <mail>")]
    public void Personal_details_never_reach_a_log(string line, string expected) => Assert.Equal(expected, Scrub(line));

    [Fact]
    public void What_AFTERLIFE_did_stays_readable()
    {
        const string line = "on  Appearance Menu Mod [pkg-0a1b2c3d4e5f] in 'Street' · archive/pc/mod/amm.archive · v2.3";
        Assert.Equal(line, Scrub(line));
        // a short user name must not eat ordinary words
        Assert.Equal("an archive", LogPrivacy.Scrub("an archive", null, null, "an", "pc", null));
    }

    [Fact]
    public void The_activity_log_is_scrubbed_on_the_way_to_the_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "afterlife-log-" + Guid.NewGuid().ToString("N"));
        var (oldDir, oldOn, oldGame) = (ActivityLog.Directory, ActivityLog.Enabled, LogPrivacy.GameRoot);
        try
        {
            ActivityLog.Directory = dir;
            ActivityLog.Enabled = true;
            LogPrivacy.GameRoot = @"D:\Games\Cyberpunk 2077";
            ActivityLog.Write("error", @"Access to the path 'D:\Games\Cyberpunk 2077\r6\scripts\x.reds' is denied. link nxm://x?key=SECRET");
            var text = File.ReadAllText(ActivityLog.CurrentFile);
            Assert.Contains(@"'<game>\r6\scripts\x.reds'", text);
            Assert.DoesNotContain("D:\\Games", text);
            Assert.DoesNotContain("SECRET", text);
        }
        finally
        {
            (ActivityLog.Directory, ActivityLog.Enabled, LogPrivacy.GameRoot) = (oldDir, oldOn, oldGame);
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
