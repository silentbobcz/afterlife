using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>
/// The two lower parts of the switch-off dialog: what is not needed any more (offered to switch off too) and what
/// stays on because another switched-on mod still needs it.
/// </summary>
public sealed record SwitchOffParts(IReadOnlyList<InactiveRequirement> Unneeded, IReadOnlyList<InactiveRequirement> Kept);

/// <summary>
/// Pure calculation over one overview. Everything is computed from the set of mods that really goes off: the mods
/// being switched off plus, when the user keeps that box ticked, the switched-on mods that need them. A mod that
/// needs them but is left on still counts as switched on, so what it needs stays on.
/// </summary>
public static class SwitchOffPlanner
{
    /// <summary>
    /// Both parts for switching off <paramref name="ids"/>; <paramref name="dependents"/> go off too only when
    /// <paramref name="withDependents"/> is true.
    /// </summary>
    public static SwitchOffParts Parts(InstallOverview o, IEnumerable<string> ids, IEnumerable<InactiveRequirement> dependents, bool withDependents)
    {
        var deps = dependents.Select(d => d.PackageId).ToList();
        var goingOff = withDependents ? ids.Concat(deps).ToList() : ids.ToList();
        // a mod that needs it and is left on is never offered as "not needed any more" (circular requirements)
        var unneeded = Unneeded(o, goingOff).Where(u => !deps.Contains(u.PackageId)).ToList();
        var kept = OverviewBuilder.KeptRequirements(o, goingOff, unneeded).Where(k => !deps.Contains(k.PackageId)).ToList();
        return new SwitchOffParts(unneeded, kept);
    }

    /// <summary>
    /// Switched-on mods that <paramref name="goingOff"/> need (files or Nexus page) and that no mod staying on needs
    /// once they are off. Core mods are never offered.
    /// </summary>
    public static IReadOnlyList<InactiveRequirement> Unneeded(InstallOverview o, IEnumerable<string> goingOff)
    {
        var off = goingOff.ToHashSet(StringComparer.Ordinal);
        var byId = o.Mods.ToDictionary(m => m.Package.PackageId, StringComparer.Ordinal);
        var result = new List<InactiveRequirement>();
        foreach (var (id, by) in RequirementsOf(o, byId, off))
        {
            if (off.Contains(id) || !byId.TryGetValue(id, out var c) || !c.Wanted || c.IsCore) continue;
            if (!StillNeeded(o, c, off)) result.Add(new InactiveRequirement(id, c.Package.ModName, by, by));
        }
        return result;
    }

    /// <summary>
    /// A mod that stays on needs <paramref name="candidate"/> and nothing else that stays on provides the same
    /// thing (same rules as <see cref="OverviewBuilder.ActiveDependents"/>).
    /// </summary>
    private static bool StillNeeded(InstallOverview o, ModOverview candidate, HashSet<string> off)
    {
        var id = candidate.Package.PackageId;
        var nexusId = NexusInfo.IdOf(candidate.Package).Id;
        var stillOn = o.Mods.Where(m => m.Wanted && m.Package.PackageId != id && !off.Contains(m.Package.PackageId)).ToList();
        foreach (var m in stillOn)
        {
            foreach (var r in m.Requirements)
            {
                if (r.ProviderPackageId != id) continue;
                if (!stillOn.Any(x => x.Package.PackageId != m.Package.PackageId && x.Package.Provides.Contains(r.Capability))) return true;
            }
            if (nexusId is null) continue;
            foreach (var r in m.RequirementLinks.Where(r => !r.External && !r.Optional && r.ModId == nexusId))
                if (!stillOn.Any(x => NexusInfo.IdOf(x.Package).Id == r.ModId)) return true;
        }
        return false;
    }

    /// <summary>Package id of every mod the given mods need (files or Nexus page), with the name of the first mod that needs it.</summary>
    private static Dictionary<string, string> RequirementsOf(InstallOverview o, Dictionary<string, ModOverview> byId, IEnumerable<string> ids)
    {
        var needs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var m)) continue;
            foreach (var r in m.Requirements)
                if (r.ProviderPackageId is { } pid) needs.TryAdd(pid, m.Package.ModName);
            foreach (var r in m.RequirementLinks.Where(r => !r.External && r.ModId is not null))
                foreach (var x in o.Mods.Where(x => NexusInfo.IdOf(x.Package).Id == r.ModId)) needs.TryAdd(x.Package.PackageId, m.Package.ModName);
        }
        return needs;
    }
}
