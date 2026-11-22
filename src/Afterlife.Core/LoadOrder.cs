namespace Afterlife.Core;

/// <summary>
/// The free load order inside MODS and its categories. The order lives in the profile (its entries, see
/// <see cref="BlockLayout.Compose"/> for the sections); these helpers move mods inside it, sort one section by name
/// and arrange the rows of the list. A layer (a mod's local edits, "└ name") has no place of its own: it is shown
/// right below its mod and moves with it.
/// </summary>
public static class LoadOrder
{
    /// <summary>Key in the meta table: profiles still shown by name once, as before the order became free.</summary>
    public const string TakeOverKey = "order.takeover";

    /// <summary>
    /// Moves the given items as one group before (or after) the target. The group keeps the order it has in
    /// <paramref name="order"/>; a target inside the group, or no item to move, leaves the order as it is.
    /// An unknown target puts the group at the end.
    /// </summary>
    public static List<string> MoveGroup(IReadOnlyList<string> order, IEnumerable<string> moved, string target, bool after)
    {
        var set = moved.ToHashSet(StringComparer.Ordinal);
        var group = order.Where(set.Contains).ToList();
        if (group.Count == 0 || set.Contains(target)) return order.ToList();
        var rest = order.Where(id => !set.Contains(id)).ToList();
        var i = rest.IndexOf(target);
        if (i < 0) i = rest.Count;
        else if (after) i++;
        rest.InsertRange(i, group);
        return rest;
    }

    /// <summary>
    /// Sorts the items of one section by name (then version) in the places that section holds; every other item
    /// keeps its place.
    /// </summary>
    public static List<string> SortSection(IReadOnlyList<string> order, Func<string, string> sectionOf, string section,
        Func<string, string> nameOf, Func<string, string> versionOf, IComparer<string> names)
    {
        var slots = new List<int>();
        for (var i = 0; i < order.Count; i++)
            if (sectionOf(order[i]) == section) slots.Add(i);
        var sorted = slots.Select(i => order[i]).OrderBy(nameOf, names).ThenBy(versionOf, StringComparer.OrdinalIgnoreCase).ToList();
        var result = order.ToList();
        for (var k = 0; k < slots.Count; k++) result[slots[k]] = sorted[k];
        return result;
    }

    /// <summary>
    /// Rows of the list in load order: by section rank, then inside a section either in the order given (the
    /// profile's order) or by name and version (<paramref name="byName"/>). A layer whose parent (by
    /// <paramref name="parentOf"/>) is in the same section follows it directly, its own layers sorted by name.
    /// </summary>
    public static List<T> Arrange<T>(IReadOnlyList<T> rows, Func<T, string> idOf, Func<T, int> rankOf, Func<T, string?> parentOf,
        Func<T, bool> byName, Func<T, string> nameOf, Func<T, string> versionOf, IComparer<string> names)
    {
        var rankById = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var r in rows) rankById.TryAdd(idOf(r), rankOf(r));
        string? AttachedTo(T r) => parentOf(r) is { } p && p != idOf(r) && rankById.TryGetValue(p, out var pr) && pr == rankOf(r) ? p : null;

        var layers = rows.Where(r => AttachedTo(r) is not null)
            .GroupBy(r => AttachedTo(r)!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(nameOf, names).ThenBy(versionOf, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.Ordinal);
        var main = rows.Select((r, i) => (Row: r, Index: i))
            .Where(x => AttachedTo(x.Row) is null)
            .OrderBy(x => rankOf(x.Row))
            .ThenBy(x => byName(x.Row) ? nameOf(x.Row) : "", names)
            .ThenBy(x => byName(x.Row) ? versionOf(x.Row) : "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Index)
            .Select(x => x.Row);

        var result = new List<T>(rows.Count);
        foreach (var r in main)
        {
            result.Add(r);
            if (layers.TryGetValue(idOf(r), out var own)) result.AddRange(own);
        }
        return result;
    }

    /// <summary>
    /// Profiles that are shown by name once more before their own order counts: a library from before the free
    /// order starts with all its profiles here (null = never set up); every other value is the stored list.
    /// </summary>
    public static HashSet<string> PendingTakeOver(string? stored, IEnumerable<string> profileIds) =>
        stored is null
            ? profileIds.ToHashSet(StringComparer.Ordinal)
            : stored.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);

    /// <summary>The stored form of <see cref="PendingTakeOver"/>: one profile id per line, sorted.</summary>
    public static string FormatTakeOver(IEnumerable<string> pending) => string.Join("\n", pending.Order(StringComparer.Ordinal));
}
