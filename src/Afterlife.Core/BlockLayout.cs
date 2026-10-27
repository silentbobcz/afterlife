using System.Text.Json;

namespace Afterlife.Core;

/// <summary>P95: a user category inside the MODS block.</summary>
public sealed record ModCategory(string Id, string Name);

/// <summary>
/// P95/P97: how the library list is split into blocks and in which order they load.
/// CORE MODS always come first and are not part of the layout. After them the blocks
/// (collections and MODS) load in <see cref="Blocks"/> order; inside MODS the mods without
/// a category load first, then every category in <see cref="Categories"/> order.
/// </summary>
public sealed record BlockLayout(
    IReadOnlyList<string> Blocks,
    IReadOnlyList<ModCategory> Categories,
    IReadOnlyDictionary<string, string> CategoryOf)
{
    public const string ModsBlock = "mods";
    public const string CollectionPrefix = "collection:";
    public const string CategoryPrefix = "category:";
    public const int MaxNameLength = 32;

    public static BlockLayout Empty { get; } = new([], [], new Dictionary<string, string>());

    public static string CollectionKey(string slug) => CollectionPrefix + slug;
    public static string CategoryKey(string id) => CategoryPrefix + id;

    /// <summary>Saved order first; collections that are new go before MODS (where they were shown so far), MODS always present.</summary>
    public IReadOnlyList<string> NormalizedBlocks(IReadOnlyList<string> collectionSlugs)
    {
        var known = collectionSlugs.Select(CollectionKey).Append(ModsBlock).ToHashSet(StringComparer.Ordinal);
        var result = Blocks.Where(known.Contains).Distinct(StringComparer.Ordinal).ToList();
        var missing = collectionSlugs.Select(CollectionKey).Where(k => !result.Contains(k)).ToList();
        var mods = result.IndexOf(ModsBlock);
        if (mods < 0) { result.AddRange(missing); result.Add(ModsBlock); }
        else result.InsertRange(mods, missing);
        return result;
    }

    /// <summary>Every section in load order: blocks, with MODS expanded into "mods" + its categories.</summary>
    public IReadOnlyList<string> Sections(IReadOnlyList<string> collectionSlugs)
    {
        var result = new List<string>();
        foreach (var b in NormalizedBlocks(collectionSlugs))
        {
            result.Add(b);
            if (b == ModsBlock) result.AddRange(Categories.Select(c => CategoryKey(c.Id)));
        }
        return result;
    }

    /// <summary>Category of a mod that is not in a collection; null = MODS without category.</summary>
    public string? CategoryIdOf(string packageId) =>
        CategoryOf.TryGetValue(packageId, out var id) && Categories.Any(c => c.Id == id) ? id : null;

    /// <summary>
    /// Load order that follows the blocks: a stable sort of the current order by section,
    /// so the order of mods inside one section does not change.
    /// </summary>
    public static List<string> Compose(IReadOnlyList<string> currentOrder, Func<string, string> sectionOf, IReadOnlyList<string> sections)
    {
        var rank = sections.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i, StringComparer.Ordinal);
        return currentOrder
            .Select((id, i) => (id, i, r: rank.TryGetValue(sectionOf(id), out var r) ? r : sections.Count))
            .OrderBy(x => x.r).ThenBy(x => x.i)
            .Select(x => x.id).ToList();
    }

    /// <summary>Moves one item before (or after) another; unknown items leave the list as it is.</summary>
    public static List<string> MoveBefore(IReadOnlyList<string> list, string item, string target, bool after)
    {
        var result = list.ToList();
        if (item == target || !result.Remove(item)) return list.ToList();
        var i = result.IndexOf(target);
        if (i < 0) return list.ToList();
        result.Insert(after ? i + 1 : i, item);
        return result;
    }

    public BlockLayout WithBlocks(IReadOnlyList<string> blocks) => this with { Blocks = blocks.ToList() };

    public BlockLayout WithCategories(IReadOnlyList<ModCategory> categories)
    {
        var ids = categories.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        return this with
        {
            Categories = categories.ToList(),
            CategoryOf = CategoryOf.Where(kv => ids.Contains(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
        };
    }

    /// <summary>Puts mods into a category (null = back to MODS without category).</summary>
    public BlockLayout Assign(IEnumerable<string> packageIds, string? categoryId)
    {
        var map = CategoryOf.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        foreach (var id in packageIds)
        {
            if (categoryId is null) map.Remove(id);
            else map[id] = categoryId;
        }
        return this with { CategoryOf = map };
    }

    /// <summary>Forgets mods that are no longer in the library.</summary>
    public BlockLayout Prune(IReadOnlySet<string> packageIds) =>
        CategoryOf.Keys.All(packageIds.Contains) ? this
            : this with { CategoryOf = CategoryOf.Where(kv => packageIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal) };

    public static string NewCategoryId() => Guid.NewGuid().ToString("N")[..10];

    // ---------------- storage ----------------
    private sealed record Dto(List<string>? Blocks, List<ModCategory>? Categories, Dictionary<string, string>? CategoryOf);
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string ToJson() => JsonSerializer.Serialize(new Dto(Blocks.ToList(), Categories.ToList(), CategoryOf.ToDictionary(kv => kv.Key, kv => kv.Value)), Options);

    public static BlockLayout FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        try
        {
            var d = JsonSerializer.Deserialize<Dto>(json, Options);
            if (d is null) return Empty;
            var cats = (d.Categories ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
                .DistinctBy(c => c.Id).ToList();
            return new BlockLayout(d.Blocks ?? [], cats, new Dictionary<string, string>(d.CategoryOf ?? [], StringComparer.Ordinal));
        }
        catch (JsonException) { return Empty; }
    }
}
