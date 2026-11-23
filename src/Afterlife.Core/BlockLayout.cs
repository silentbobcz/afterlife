using System.Text.Json;

namespace Afterlife.Core;

/// <summary>A user category inside the MODS block.</summary>
public sealed record ModCategory(string Id, string Name);

/// <summary>
/// How the library list is split into blocks and in which order they load.
/// CORE MODS always come first and are not part of the layout. After them the blocks
/// (NEW, collections and MODS) load in <see cref="Blocks"/> order; inside MODS the mods without
/// A category load first, then every category in <see cref="Categories"/> order.
/// NEW holds freshly installed mods (<see cref="NewIds"/>) until the user moves them; empty, it changes nothing.
/// </summary>
public sealed record BlockLayout(
    IReadOnlyList<string> Blocks,
    IReadOnlyList<ModCategory> Categories,
    IReadOnlyDictionary<string, string> CategoryOf)
{
    public const string ModsBlock = "mods";
    /// <summary>Block of freshly installed mods; first unless the user moved it.</summary>
    public const string NewBlock = "new";
    public const string CollectionPrefix = "collection:";
    public const string CategoryPrefix = "category:";
    public const int MaxNameLength = 32;
    /// <summary>Key of the saved layout in the meta table of the library.</summary>
    public const string StoreKey = "ui.layout";

    // declared before Empty: static fields are set in the order they are written
    private static readonly HashSet<string> NoIds = new(StringComparer.Ordinal);

    public static BlockLayout Empty { get; } = new([], [], new Dictionary<string, string>());

    /// <summary>Mods in the NEW block (not in a collection, no category); empty in layouts saved before NEW existed.</summary>
    public IReadOnlySet<string> NewIds { get; init; } = NoIds;

    public static string CollectionKey(string slug) => CollectionPrefix + slug;
    public static string CategoryKey(string id) => CategoryPrefix + id;

    /// <summary>
    /// Saved order first; collections that are new go before MODS (where they were shown so far), MODS always present,
    /// NEW where it was saved, or first when the saved order does not know it yet.
    /// </summary>
    public IReadOnlyList<string> NormalizedBlocks(IReadOnlyList<string> collectionSlugs)
    {
        var known = collectionSlugs.Select(CollectionKey).Append(ModsBlock).Append(NewBlock).ToHashSet(StringComparer.Ordinal);
        var result = Blocks.Where(known.Contains).Distinct(StringComparer.Ordinal).ToList();
        var missing = collectionSlugs.Select(CollectionKey).Where(k => !result.Contains(k)).ToList();
        var mods = result.IndexOf(ModsBlock);
        if (mods < 0) { result.AddRange(missing); result.Add(ModsBlock); }
        else result.InsertRange(mods, missing);
        if (!result.Contains(NewBlock)) result.Insert(0, NewBlock);
        return result;
    }

    /// <summary>Every section in load order: blocks (NEW included), with MODS expanded into "mods" + its categories.</summary>
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
    /// So the order of mods inside one section does not change.
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

    /// <summary>Puts mods into a category (null = back to MODS without category); they leave NEW.</summary>
    public BlockLayout Assign(IEnumerable<string> packageIds, string? categoryId)
    {
        var ids = packageIds.ToList();
        var map = CategoryOf.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (categoryId is null) map.Remove(id);
            else map[id] = categoryId;
        }
        return RemoveNew(ids) with { CategoryOf = map };
    }

    public bool IsNew(string packageId) => NewIds.Contains(packageId);

    /// <summary>Puts freshly installed mods into NEW.</summary>
    public BlockLayout AddNew(IEnumerable<string> packageIds)
    {
        var added = packageIds.Where(id => !NewIds.Contains(id)).ToList();
        if (added.Count == 0) return this;
        var set = new HashSet<string>(NewIds, StringComparer.Ordinal);
        set.UnionWith(added);
        return this with { NewIds = set };
    }

    /// <summary>Takes mods out of NEW; they become ordinary mods of MODS (or of their category).</summary>
    public BlockLayout RemoveNew(IEnumerable<string> packageIds)
    {
        var removed = packageIds.Where(NewIds.Contains).ToList();
        if (removed.Count == 0) return this;
        var set = new HashSet<string>(NewIds, StringComparer.Ordinal);
        set.ExceptWith(removed);
        return this with { NewIds = set.Count == 0 ? NoIds : set };
    }

    /// <summary>Forgets mods that are no longer in the library.</summary>
    public BlockLayout Prune(IReadOnlySet<string> packageIds)
    {
        var categoriesKept = CategoryOf.Keys.All(packageIds.Contains);
        var gone = NewIds.Where(id => !packageIds.Contains(id)).ToList();
        if (categoriesKept && gone.Count == 0) return this;
        return RemoveNew(gone) with
        {
            CategoryOf = categoriesKept ? CategoryOf
                : CategoryOf.Where(kv => packageIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
        };
    }

    public static string NewCategoryId() => Guid.NewGuid().ToString("N")[..10];

    // ---------------- storage ----------------
    // NewIds is left out while NEW is empty, so a layout without NEW mods is stored exactly as before
    private sealed record Dto(List<string>? Blocks, List<ModCategory>? Categories, Dictionary<string, string>? CategoryOf, List<string>? NewIds = null);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(new Dto(Blocks.ToList(), Categories.ToList(), CategoryOf.ToDictionary(kv => kv.Key, kv => kv.Value),
        NewIds.Count == 0 ? null : NewIds.Order(StringComparer.Ordinal).ToList()), Options);

    public static BlockLayout FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        try
        {
            var d = JsonSerializer.Deserialize<Dto>(json, Options);
            if (d is null) return Empty;
            var cats = (d.Categories ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
                .DistinctBy(c => c.Id).ToList();
            return new BlockLayout(d.Blocks ?? [], cats, new Dictionary<string, string>(d.CategoryOf ?? [], StringComparer.Ordinal))
            {
                NewIds = d.NewIds is { Count: > 0 } fresh ? fresh.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal) : NoIds,
            };
        }
        catch (JsonException) { return Empty; }
    }
}
