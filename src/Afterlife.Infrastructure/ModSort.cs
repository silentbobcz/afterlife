using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>How the mod list is sorted for the eye. Only <see cref="LoadOrder"/> is the order the game loads.</summary>
public enum ListSort { LoadOrder, Name, Added, State, Size }

/// <summary>
/// Sorting of the mod list for viewing only. The load order lives in the profile and is never derived from a
/// sorted list: the window keeps the rows in load order beside the ones it shows. Pure, so it can be tested
/// without the window.
/// </summary>
public static class ModSort
{
    public static string Label(ListSort sort) => sort switch
    {
        ListSort.Name => "NAME",
        ListSort.Added => "NEWEST",
        ListSort.State => "STATE",
        ListSort.Size => "SIZE",
        _ => "LOAD ORDER",
    };

    public static ListSort Next(ListSort sort) => sort == ListSort.Size ? ListSort.LoadOrder : sort + 1;

    /// <summary>What needs attention first, then what waits for apply, then on, then off.</summary>
    public static int StateRank(ModOverview o) => o.State switch
    {
        ModState.MissingRequirement => 0,
        ModState.Conflict => 1,
        ModState.PendingAdd or ModState.PendingUpdate or ModState.PendingRemoval => 3,
        _ when o.Missing.Count > 0 => 0,
        _ when o.Update is not null => 2,
        _ => o.Wanted ? 4 : 5,
    };

    public static long SizeOf(PackageManifest p) => p.Files.Sum(f => f.Size);

    /// <summary>
    /// The rows as shown: blocks stay where they are and in their order, fixed rows (core mods) keep their place,
    /// the rest is sorted inside its block. A layer ("my edits") stays right below its mod. The input is in load
    /// order and is returned as it is for <see cref="ListSort.LoadOrder"/>.
    /// </summary>
    /// <param name="block">Position of the row's block in the list.</param>
    /// <param name="isFixed">Rows that are never sorted.</param>
    public static IReadOnlyList<T> View<T>(IReadOnlyList<T> loadOrdered, ListSort sort, Func<T, ModOverview> of, Func<T, int> block, Func<T, bool> isFixed)
        where T : class
    {
        if (sort == ListSort.LoadOrder) return loadOrdered;
        var parents = new Dictionary<string, ModOverview>(StringComparer.Ordinal);
        foreach (var r in loadOrdered)
            if (of(r).Package.LayerOfModId is null) parents.TryAdd(of(r).Package.ModId, of(r));
        // a layer sorts by its mod, so it stays below it whatever the key
        ModOverview Anchor(T r) => of(r).Package.LayerOfModId is { } id && parents.TryGetValue(id, out var p) ? p : of(r);
        var position = new Dictionary<T, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < loadOrdered.Count; i++) position[loadOrdered[i]] = i;
        var byName = StringComparer.Create(System.Globalization.CultureInfo.CurrentCulture, ignoreCase: true);

        var sorted = loadOrdered.Where(r => !isFixed(r)).OrderBy(block);
        sorted = sort switch
        {
            ListSort.Added => sorted.ThenByDescending(r => Anchor(r).Package.ImportedAt ?? DateTimeOffset.MinValue),
            ListSort.State => sorted.ThenBy(r => StateRank(Anchor(r))),
            ListSort.Size => sorted.ThenByDescending(r => SizeOf(Anchor(r).Package)),
            _ => sorted,
        };
        var movable = sorted.ThenBy(r => Anchor(r).Package.ModName, byName)
            .ThenBy(r => Anchor(r).Package.PackageId, StringComparer.Ordinal)
            .ThenBy(r => of(r).Package.LayerOfModId is null ? 0 : 1)
            .ThenBy(r => position[r]).ToList();

        // fixed rows keep their index; the sorted ones fill the remaining places of their block in order
        var result = new List<T>(loadOrdered.Count);
        var next = movable.GroupBy(block).ToDictionary(g => g.Key, g => new Queue<T>(g));
        foreach (var r in loadOrdered)
            result.Add(isFixed(r) ? r : next[block(r)].Dequeue());
        return result;
    }
}
