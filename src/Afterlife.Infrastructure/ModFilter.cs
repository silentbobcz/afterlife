namespace Afterlife.Infrastructure;

/// <summary>A filter item is off, must match, or must not match.</summary>
public enum FilterState { Off, Include, Exclude }

/// <summary>Where a mod sits in the list, for the BLOCK filters.</summary>
public enum ModBlock { Core, Collection, Mods }

/// <summary>
/// The filter panel of the mod list. A mod is described by a set of facet keys ("state:on", "need:update",
/// "block:core", "tag:Archive"); the panel holds a state per key. Pure, so it can be tested without the window.
/// </summary>
public static class ModFilter
{
    public const string On = "state:on", Off = "state:off", Pending = "state:pending";
    public const string Update = "need:update", Conflict = "need:conflict", Missing = "need:missing";
    public const string Core = "block:core", Collection = "block:collection", Mods = "block:mods";
    public static string Tag(string label) => "tag:" + label;

    public static HashSet<string> Facets(ModOverview o, ModBlock block, IEnumerable<string> tags)
    {
        var f = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { o.Wanted ? On : Off };
        if (o.State is ModState.PendingAdd or ModState.PendingUpdate or ModState.PendingRemoval) f.Add(Pending);
        if (o.Update is not null) f.Add(Update);
        if (o.State == ModState.Conflict) f.Add(Conflict);
        if (o.State == ModState.MissingRequirement || o.Missing.Count > 0) f.Add(Missing);
        f.Add(block switch { ModBlock.Core => Core, ModBlock.Collection => Collection, _ => Mods });
        foreach (var t in tags) f.Add(Tag(t));
        return f;
    }

    /// <summary>Excluded facets always hide; included ones must all match (matchAll) or at least one must.</summary>
    public static bool Matches(IReadOnlySet<string> facets, IReadOnlyDictionary<string, FilterState> states, bool matchAll)
    {
        var any = false;
        var anyHit = false;
        foreach (var (key, state) in states)
        {
            if (state == FilterState.Exclude && facets.Contains(key)) return false;
            if (state != FilterState.Include) continue;
            var hit = facets.Contains(key);
            if (matchAll && !hit) return false;
            any = true;
            anyHit |= hit;
        }
        return !any || matchAll || anyHit;
    }

    public static FilterState Next(FilterState s) => s switch
    {
        FilterState.Off => FilterState.Include,
        FilterState.Include => FilterState.Exclude,
        _ => FilterState.Off,
    };
}
