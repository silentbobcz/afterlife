namespace Afterlife.Infrastructure;

/// <summary>Which mods an update check looks at.</summary>
public enum UpdateScope
{
    /// <summary>Core mods only: the few everything else builds on.</summary>
    CoreOnly,
    /// <summary>Core mods and the mods switched on in the selected profile.</summary>
    Active,
    All,
}

/// <summary>One linked package as the update check sees it.</summary>
/// <param name="CheckedAt">Last check of any kind, also "the feed says nothing changed".</param>
/// <param name="FullReadAt">Last time the file list of the mod was really read.</param>
public sealed record UpdateItem(string PackageId, string ModId, DateTimeOffset? CheckedAt, DateTimeOffset? FullReadAt, bool IsCore, bool IsActive);

/// <summary>
/// Decides which mods an update check reads, and in which order. Between full reads a mod is only asked about
/// through the "updated mods" feed of Nexus Mods (one request for all mods). As a safety net every mod is read
/// in full once in a while, each on its own day, so the reads never pile up. Pure, so it can be tested without Nexus.
/// </summary>
public static class UpdatePlan
{
    private static string Key(string packageId) => "nexus:fullread:" + packageId;

    public static bool InScope(UpdateItem i, UpdateScope scope) => scope switch
    {
        UpdateScope.CoreOnly => i.IsCore,
        UpdateScope.Active => i.IsCore || i.IsActive,
        _ => true,
    };

    /// <summary>Core mods first, then the mods in the profile, then the rest.</summary>
    public static int Priority(UpdateItem i) => i.IsCore ? 0 : i.IsActive ? 1 : 2;

    /// <summary>
    /// Where in the period the full read of this package falls. Derived from the id, so it is the same on every
    /// start and spreads the packages evenly over the period.
    /// </summary>
    public static TimeSpan Phase(string packageId, TimeSpan every)
    {
        if (every <= TimeSpan.Zero) return TimeSpan.Zero;
        uint h = 2166136261;
        foreach (var c in packageId) h = (h ^ c) * 16777619;
        return TimeSpan.FromSeconds(h % (ulong)every.TotalSeconds);
    }

    /// <summary>
    /// The package passed its own point of the period since its last full read. A package never read in full
    /// counts from its last check, so a library checked before this rule existed is spread over one period
    /// instead of being read at once. <paramref name="every"/> zero or less switches full reads off.
    /// </summary>
    public static bool FullReadDue(UpdateItem i, TimeSpan every, DateTimeOffset now)
    {
        if (every <= TimeSpan.Zero) return false;
        if ((i.FullReadAt ?? i.CheckedAt) is not { } last) return true;
        var phase = Phase(i.PackageId, every);
        long Slot(DateTimeOffset t) => (long)Math.Floor((t - DateTimeOffset.UnixEpoch - phase) / every);
        return Slot(now) > Slot(last);
    }

    /// <summary>
    /// Packages whose files must be read now, most important first: never checked, changed on Nexus since the
    /// last check (feed), older than the feed reaches, or due for their full read. Without the feed: not checked
    /// for a day.
    /// </summary>
    public static IReadOnlyList<UpdateItem> Due(IEnumerable<UpdateItem> items, IReadOnlyDictionary<string, long>? feed, UpdateScope scope,
        TimeSpan fullEvery, DateTimeOffset now)
    {
        var inScope = items.Where(i => InScope(i, scope)).ToList();
        var byFeed = feed is null
            ? inScope.Where(i => i.CheckedAt is not { } at || now - at >= TimeSpan.FromHours(24)).Select(i => i.PackageId)
            : NexusUpdates.DueAfterFeed(inScope.Select(i => (i.PackageId, i.ModId, i.CheckedAt)), feed, now);
        var due = byFeed.ToHashSet(StringComparer.Ordinal);
        return inScope.Where(i => due.Contains(i.PackageId) || FullReadDue(i, fullEvery, now)).OrderBy(Priority).ToList();
    }

    public static void MarkFullRead(AfterlifeStore store, string packageId, DateTimeOffset now) =>
        store.SetMeta(Key(packageId), now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>When the files of every package were last read, by package id, in one query.</summary>
    public static Dictionary<string, DateTimeOffset> FullReadAll(AfterlifeStore store)
    {
        var prefix = Key("");
        var result = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var (key, value) in store.GetMetaByPrefix(prefix))
            if (long.TryParse(value, out var t)) result[key[prefix.Length..]] = DateTimeOffset.FromUnixTimeSeconds(t);
        return result;
    }
}
