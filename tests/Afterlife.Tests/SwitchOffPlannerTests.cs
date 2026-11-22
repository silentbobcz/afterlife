using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

/// <summary>
/// The parts NOT NEEDED ANY MORE and STAYS ON of the switch-off dialog follow the set that really goes off:
/// the mod plus, when ticked, the mods that need it.
/// </summary>
public class SwitchOffPlannerTests
{
    /// <summary>A plain switched-on mod linked to a Nexus page; others can list that page as a requirement.</summary>
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
    public void Switching_off_the_dependents_too_frees_their_shared_requirement()
    {
        using var t = new TestEnv();
        var lib = Linked(t, "L", "9001");
        var a = Linked(t, "A", "9002", "9001");
        var b = Linked(t, "B", "9003", "9001", "9002");
        var o = Overview(t);
        var dependents = OverviewBuilder.ActiveDependents(o, [a.PackageId]);
        Assert.Equal(b.PackageId, Assert.Single(dependents).PackageId);

        // B goes off with A: nothing switched on needs L any more
        var ticked = SwitchOffPlanner.Parts(o, [a.PackageId], dependents, withDependents: true);
        Assert.Equal(lib.PackageId, Assert.Single(ticked.Unneeded).PackageId);
        Assert.Empty(ticked.Kept);

        // B stays on and still needs L
        var unticked = SwitchOffPlanner.Parts(o, [a.PackageId], dependents, withDependents: false);
        Assert.Empty(unticked.Unneeded);
        var kept = Assert.Single(unticked.Kept);
        Assert.Equal(lib.PackageId, kept.PackageId);
        Assert.Equal("also needed by B", kept.What);
    }

    [Fact]
    public void A_dependent_left_on_is_never_offered_as_not_needed()
    {
        using var t = new TestEnv();
        var a = Linked(t, "A", "9002", "9003");
        var b = Linked(t, "B", "9003", "9002");
        var o = Overview(t);
        var dependents = OverviewBuilder.ActiveDependents(o, [a.PackageId]);
        Assert.Equal(b.PackageId, Assert.Single(dependents).PackageId);
        var parts = SwitchOffPlanner.Parts(o, [a.PackageId], dependents, withDependents: false);
        Assert.Empty(parts.Unneeded);
        Assert.Empty(parts.Kept);
    }

    [Fact]
    public void Core_mods_are_never_offered_whatever_goes_off()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/version.dll", "v"));
        var mod = t.ImportZip("Lua mod", ("bin/x64/plugins/cyber_engine_tweaks/mods/Lua/init.lua", "x"));
        t.C.SetEnabled(cet.PackageId, true);
        t.C.SetEnabled(mod.PackageId, true);
        var o = Overview(t);
        foreach (var withDependents in new[] { true, false })
        {
            var parts = SwitchOffPlanner.Parts(o, [mod.PackageId], [], withDependents);
            Assert.Empty(parts.Unneeded);
            Assert.Empty(parts.Kept);
        }
    }
}
