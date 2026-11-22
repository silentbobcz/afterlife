namespace Afterlife.Core;

/// <summary>How a keyboard move shifts mods inside their section: one row up or down, or to its start or end.</summary>
public enum MoveStep { Up, Down, ToStart, ToEnd }

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
    /// Moves the given items inside their own section (<paramref name="sectionOf"/>) by one place or to its start or
    /// end; the section keeps the places it holds in <paramref name="order"/>, so no item crosses a section boundary.
    /// Moved items keep their order among themselves, a block of them at the edge stays put. Items of several
    /// sections each move inside theirs; unknown items are ignored. Returns the new order (equal to the old one when
    /// nothing moves).
    /// </summary>
    public static List<string> MoveInSection(IReadOnlyList<string> order, IEnumerable<string> moved, Func<string, string> sectionOf, MoveStep step)
    {
        var set = moved.ToHashSet(StringComparer.Ordinal);
        var result = order.ToList();
        foreach (var section in order.Where(set.Contains).Select(sectionOf).Distinct(StringComparer.Ordinal).ToList())
        {
            var slots = new List<int>();
            for (var i = 0; i < order.Count; i++)
                if (sectionOf(order[i]) == section) slots.Add(i);
            var ids = slots.Select(i => order[i]).ToList();
            var flags = ids.Select(id => set.Contains(id)).ToList();
            switch (step)
            {
                case MoveStep.Up:
                    for (var i = 1; i < ids.Count; i++)
                        if (flags[i] && !flags[i - 1])
                        {
                            (ids[i], ids[i - 1]) = (ids[i - 1], ids[i]);
                            (flags[i], flags[i - 1]) = (flags[i - 1], flags[i]);
                        }
                    break;
                case MoveStep.Down:
                    for (var i = ids.Count - 2; i >= 0; i--)
                        if (flags[i] && !flags[i + 1])
                        {
                            (ids[i], ids[i + 1]) = (ids[i + 1], ids[i]);
                            (flags[i], flags[i + 1]) = (flags[i + 1], flags[i]);
                        }
                    break;
                case MoveStep.ToStart:
                    ids = ids.Where(id => set.Contains(id)).Concat(ids.Where(id => !set.Contains(id))).ToList();
                    break;
                default:
                    ids = ids.Where(id => !set.Contains(id)).Concat(ids.Where(id => set.Contains(id))).ToList();
                    break;
            }
            for (var k = 0; k < slots.Count; k++) result[slots[k]] = ids[k];
        }
        return result;
    }

    /// <summary>
    /// A file the load order decides: an <c>.archive</c> directly in <c>archive/pc/mod</c> (a package file's target,
    /// a game path with forward slashes).
    /// </summary>
    public static bool IsOrderedArchive(string target)
    {
        const string folder = "archive/pc/mod/";
        var t = target.Replace('\\', '/');
        return t.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
            && t.EndsWith(".archive", StringComparison.OrdinalIgnoreCase)
            && t.IndexOf('/', folder.Length) < 0;
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
