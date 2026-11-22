namespace Afterlife.Core;

/// <summary>
/// A package of the library as an import of a load list sees it. <see cref="Section"/> is where the window shows it
/// now: <c>core</c>, <c>collection:&lt;slug&gt;</c>, <c>mods</c> or <c>category:&lt;id&gt;</c>.
/// </summary>
public sealed record LoadListPackage(
    string PackageId,
    string Name,
    string Version,
    string Section,
    bool Enabled = false,
    bool IsCore = false,
    bool IsLayer = false,
    string? NexusModId = null,
    string? NexusFileId = null,
    string? FileName = null,
    string? Md5 = null,
    DateTimeOffset? ImportedAt = null);

/// <summary>A collection of the library; <see cref="Locked"/> when it fixes the order in the selected profile.</summary>
public sealed record LoadListCollection(string Slug, string Name, bool Locked);

/// <summary>The choices of the preview (D5, D3, D4 in the format proposal).</summary>
public sealed record LoadListOptions(bool SwitchOnOff = true, bool SwitchOffOthers = false, bool UnlockCollections = false);

/// <summary>How a line of the list was found in the library, in the order the steps are tried.</summary>
public enum LoadListMatchKind { NexusFile, Md5, FileName, NameAndVersion, NexusMod, NameOnly }

public sealed record LoadListMatch(LoadListMod Line, LoadListPackage Package, LoadListMatchKind Kind)
{
    /// <summary>The list names a version and the library has another one.</summary>
    public bool OtherVersion => Line.Version is { } v && !LoadListPlanner.Same(v, Package.Version);
}

/// <summary>
/// What an import of a load list will do and what its preview shows. The preview and the import use the same plan,
/// so what is shown is exactly what is written.
/// </summary>
public sealed class LoadListPlan
{
    /// <summary>Load order of every mod outside CORE MODS (layers excluded), grouped by the sections of <see cref="Layout"/>.</summary>
    public IReadOnlyList<string> Order { get; init; } = [];
    public BlockLayout Layout { get; init; } = BlockLayout.Empty;
    public IReadOnlyList<string> SwitchOn { get; init; } = [];
    public IReadOnlyList<string> SwitchOff { get; init; } = [];
    /// <summary>Collections whose order lock is taken off (only with <see cref="LoadListOptions.UnlockCollections"/>).</summary>
    public IReadOnlyList<string> Unlock { get; init; } = [];

    public IReadOnlyList<LoadListMatch> Matches { get; init; } = [];
    /// <summary>Names of the mods that change place.</summary>
    public IReadOnlyList<string> Moved { get; init; } = [];
    public IReadOnlyList<string> SwitchOnNames { get; init; } = [];
    public IReadOnlyList<string> SwitchOffNames { get; init; } = [];
    public IReadOnlyList<string> NewCategories { get; init; } = [];
    /// <summary>"long name → cut name" for category names longer than <see cref="BlockLayout.MaxNameLength"/>.</summary>
    public IReadOnlyList<string> CutNames { get; init; } = [];
    /// <summary>"mod → category" for mods that move into another category (or back to MODS).</summary>
    public IReadOnlyList<string> CategoryMoves { get; init; } = [];
    public IReadOnlyList<LoadListMatch> OtherVersions { get; init; } = [];
    public IReadOnlyList<LoadListMatch> NameOnly { get; init; } = [];
    public IReadOnlyList<LoadListMod> Missing { get; init; } = [];
    /// <summary>Later lines of a mod listed twice; the first line counts.</summary>
    public IReadOnlyList<LoadListMod> Duplicates { get; init; } = [];
    /// <summary>Switched-on mods of the profile that the list does not name.</summary>
    public IReadOnlyList<string> NotInList { get; init; } = [];
    /// <summary>Mods whose place the library decides (collection membership comes from Nexus): "name – why".</summary>
    public IReadOnlyList<string> StayOutside { get; init; } = [];
    /// <summary>Names of locked collections that keep the curator's order.</summary>
    public IReadOnlyList<string> LockedKept { get; init; } = [];
    /// <summary>Collection sections the library does not have; their mods go to a category "name (collection)".</summary>
    public IReadOnlyList<LoadListSection> MissingCollections { get; init; } = [];
    /// <summary>The list has sections in an order the window cannot show (a category above MODS, a collection between categories).</summary>
    public bool SectionOrderNote { get; init; }
    /// <summary>The profile still has the game's default order; the import gives it its own.</summary>
    public bool TakesOwnOrder { get; init; }
    /// <summary>The list names mods, but none of them is in the library.</summary>
    public bool NothingFound { get; init; }
    public bool OrderChanged { get; init; }
    public bool LayoutChanged { get; init; }

    public bool IsEmpty => !OrderChanged && !LayoutChanged && !TakesOwnOrder && SwitchOn.Count == 0 && SwitchOff.Count == 0 && Unlock.Count == 0;
}

/// <summary>
/// Finds the mods of a load list in the library and plans the import (docs/previews/load-list-format.md, sections 3
/// and 4). Pure: no database, no files.
/// </summary>
public static class LoadListPlanner
{
    public const string CoreSection = "core";

    internal static bool Same(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Name of the category made for a collection the library does not have.</summary>
    public static string CollectionCategoryName(string collection) => collection.Trim() + " (collection)";

    /// <summary>Cuts a category name to <see cref="BlockLayout.MaxNameLength"/>.</summary>
    public static string CutName(string name)
    {
        var n = name.Trim();
        return n.Length <= BlockLayout.MaxNameLength ? n : n[..BlockLayout.MaxNameLength].TrimEnd();
    }

    /// <summary>
    /// The package a line stands for: exact Nexus file, archive checksum, archive name, name and version, the same
    /// Nexus mod, name only. Several candidates: the one switched on, else the newest. Layers are never matched.
    /// </summary>
    public static LoadListMatch? Match(LoadListMod line, IReadOnlyList<LoadListPackage> packages)
    {
        var pool = packages.Where(p => !p.IsLayer).ToList();
        if (line.NexusModId is { } mod && line.NexusFileId is { } file
            && Best(pool.Where(p => p.NexusModId == mod && p.NexusFileId == file)) is { } byFile)
            return new LoadListMatch(line, byFile, LoadListMatchKind.NexusFile);
        if (line.Md5 is { } md5 && Best(pool.Where(p => Same(p.Md5, md5))) is { } byMd5)
            return new LoadListMatch(line, byMd5, LoadListMatchKind.Md5);
        if (line.FileName is { } name && Best(pool.Where(p => Same(p.FileName, name))) is { } byArchive)
            return new LoadListMatch(line, byArchive, LoadListMatchKind.FileName);
        // a mod linked to another Nexus mod is never taken for this one by its name
        var named = pool.Where(p => Same(p.Name, line.Name) && (line.NexusModId is null || p.NexusModId is null || p.NexusModId == line.NexusModId)).ToList();
        if (line.Version is { } version && Best(named.Where(p => Same(p.Version, version))) is { } byVersion)
            return new LoadListMatch(line, byVersion, LoadListMatchKind.NameAndVersion);
        if (line.NexusModId is { } mod2 && Best(pool.Where(p => p.NexusModId == mod2)) is { } byMod)
            return new LoadListMatch(line, byMod, LoadListMatchKind.NexusMod);
        return Best(named) is { } byName ? new LoadListMatch(line, byName, LoadListMatchKind.NameOnly) : null;
    }

    private static LoadListPackage? Best(IEnumerable<LoadListPackage> candidates) =>
        candidates.OrderByDescending(p => p.Enabled)
            .ThenByDescending(p => p.ImportedAt ?? DateTimeOffset.MinValue)
            .ThenBy(p => p.PackageId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// Plans the import of <paramref name="doc"/> into the selected profile. <paramref name="packages"/> are the mods
    /// of the library in the load order the window shows (core mods and layers may be anywhere; they are not ordered).
    /// </summary>
    public static LoadListPlan Build(LoadListDocument doc, IReadOnlyList<LoadListPackage> packages, BlockLayout layout,
        IReadOnlyList<LoadListCollection> collections, bool orderIsCustom, LoadListOptions options, Func<string>? newCategoryId = null)
    {
        newCategoryId ??= () => BlockLayout.NewCategoryId();
        var currentOrder = packages.Where(p => !p.IsCore && !p.IsLayer).Select(p => p.PackageId).ToList();
        var byId = new Dictionary<string, LoadListPackage>(StringComparer.Ordinal);
        foreach (var p in packages) byId.TryAdd(p.PackageId, p);

        // ---- find every line (case 11: the first line of a mod counts) ----
        var matches = new List<(int Section, LoadListMatch Match)>();
        var missing = new List<LoadListMod>();
        var duplicates = new List<LoadListMod>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var modLines = 0;
        for (var si = 0; si < doc.Sections.Count; si++)
            foreach (var line in doc.Sections[si].Mods)
            {
                modLines++;
                var m = Match(line, packages);
                if (m is null)
                {
                    if (missing.Any(x => Same(x.Name, line.Name) && x.NexusModId == line.NexusModId)) duplicates.Add(line);
                    else missing.Add(line);
                }
                else if (!used.Add(m.Package.PackageId)) duplicates.Add(line);
                else matches.Add((si, m));
            }
        if (modLines > 0 && matches.Count == 0)
            return new LoadListPlan { Order = currentOrder, Layout = layout, Missing = missing, Duplicates = duplicates, NothingFound = true };

        // ---- collections of the list: in the library, or a category instead (D1) ----
        var collectionOf = new Dictionary<int, LoadListCollection>();
        var missingCollections = new List<LoadListSection>();
        for (var si = 0; si < doc.Sections.Count; si++)
        {
            var s = doc.Sections[si];
            if (s.Kind != LoadListSectionKind.Collection) continue;
            var found = (s.Slug is { } slug ? collections.FirstOrDefault(c => string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase)) : null)
                        ?? collections.FirstOrDefault(c => Same(c.Name, s.Name));
            if (found is not null) collectionOf[si] = found;
            else missingCollections.Add(s);
        }

        static bool InModsFamily(LoadListPackage p) =>
            !p.IsCore && (p.Section == BlockLayout.ModsBlock || p.Section.StartsWith(BlockLayout.CategoryPrefix, StringComparison.Ordinal));

        // ---- categories: found by name, created where the list has them (case 1, 12) ----
        var categories = layout.Categories.ToList();
        var categoryOf = new Dictionary<int, string>(); // section index → category id
        var newCategories = new List<string>();
        var cutNames = new List<string>();
        var fileCategories = new List<string>();
        for (var si = 0; si < doc.Sections.Count; si++)
        {
            var s = doc.Sections[si];
            string wanted;
            if (s.Kind == LoadListSectionKind.Category) wanted = s.Name.Trim();
            else if (s.Kind == LoadListSectionKind.Collection && !collectionOf.ContainsKey(si))
            {
                // a category for a missing collection only when it gets mods
                if (!matches.Any(x => x.Section == si && InModsFamily(x.Match.Package))) continue;
                wanted = CollectionCategoryName(s.Name);
            }
            else continue;
            var cut = CutName(wanted);
            if (cut != wanted && !cutNames.Contains($"{wanted} → {cut}")) cutNames.Add($"{wanted} → {cut}");
            var category = categories.FirstOrDefault(c => Same(c.Name, cut));
            if (category is null)
            {
                category = new ModCategory(newCategoryId(), cut);
                categories.Add(category);
                newCategories.Add(cut);
            }
            categoryOf[si] = category.Id;
            if (!fileCategories.Contains(category.Id)) fileCategories.Add(category.Id);
        }
        var categoryById = categories.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var currentCategoryIds = layout.Categories.Select(c => c.Id).ToList();
        var mergedCategoryIds = MergeOrder(currentCategoryIds, fileCategories);
        var newLayout = mergedCategoryIds.SequenceEqual(currentCategoryIds)
            ? layout
            : layout.WithCategories(mergedCategoryIds.Select(id => categoryById[id]).ToList());

        // ---- blocks: collections and MODS in the order of the list; MODS where the first @mods or @category is ----
        var slugs = collections.Select(c => c.Slug).ToList();
        var fileBlocks = new List<string>();
        for (var si = 0; si < doc.Sections.Count; si++)
        {
            var s = doc.Sections[si];
            string? block = collectionOf.TryGetValue(si, out var c) ? BlockLayout.CollectionKey(c.Slug)
                : s.Kind == LoadListSectionKind.Mods || categoryOf.ContainsKey(si) ? BlockLayout.ModsBlock
                : null;
            if (block is not null && !fileBlocks.Contains(block)) fileBlocks.Add(block);
        }
        var currentBlocks = layout.NormalizedBlocks(slugs);
        var mergedBlocks = MergeOrder(currentBlocks, fileBlocks);
        if (!mergedBlocks.SequenceEqual(currentBlocks)) newLayout = newLayout.WithBlocks(mergedBlocks);

        // ---- where every found mod goes (cases 6, 7, 9) ----
        var placed = new Dictionary<string, List<string>>(StringComparer.Ordinal); // section → mods in the order of the list
        var stayOutside = new List<string>();
        var categoryMoves = new List<string>();
        foreach (var (si, m) in matches)
        {
            var s = doc.Sections[si];
            var p = m.Package;
            if (p.IsCore || s.Kind == LoadListSectionKind.Core || p.IsLayer) continue; // core mods always load first
            string target;
            if (collectionOf.TryGetValue(si, out var col))
            {
                target = BlockLayout.CollectionKey(col.Slug);
                if (p.Section != target) { stayOutside.Add($"{p.Name} – not part of {col.Name} in your library"); continue; }
            }
            else
            {
                if (!InModsFamily(p)) { stayOutside.Add($"{p.Name} – part of a collection in your library"); continue; }
                string? category = categoryOf.TryGetValue(si, out var id) ? id : null;
                if (s.Kind == LoadListSectionKind.Collection && category is null) continue;
                target = category is null ? BlockLayout.ModsBlock : BlockLayout.CategoryKey(category);
                if (layout.CategoryIdOf(p.PackageId) != category)
                    categoryMoves.Add($"{p.Name} → {(category is null ? "MODS" : categoryById[category].Name)}");
                newLayout = newLayout.Assign([p.PackageId], category);
            }
            if (!placed.TryGetValue(target, out var list)) placed[target] = list = [];
            list.Add(p.PackageId);
        }

        string NewSection(string id)
        {
            var p = byId[id];
            if (!InModsFamily(p)) return p.Section;
            return newLayout.CategoryIdOf(id) is { } cat ? BlockLayout.CategoryKey(cat) : BlockLayout.ModsBlock;
        }

        // ---- the order: inside a section the list's mods first, then the others as they were (D3, D4) ----
        var lockedBySection = new Dictionary<string, LoadListCollection>(StringComparer.Ordinal);
        foreach (var lc in collections) lockedBySection.TryAdd(BlockLayout.CollectionKey(lc.Slug), lc);
        var order = new List<string>();
        var unlock = new List<string>();
        var lockedKept = new List<string>();
        foreach (var section in newLayout.Sections(slugs))
        {
            var current = currentOrder.Where(id => NewSection(id) == section).ToList();
            placed.TryGetValue(section, out var fromList);
            if (fromList is { Count: > 0 } && lockedBySection.TryGetValue(section, out var col) && col.Locked)
            {
                if (!options.UnlockCollections) { lockedKept.Add(col.Name); order.AddRange(current); continue; }
                unlock.Add(col.Slug);
            }
            if (fromList is null) { order.AddRange(current); continue; }
            order.AddRange(fromList);
            order.AddRange(current.Where(id => !fromList.Contains(id)));
        }
        var inOrder = order.ToHashSet(StringComparer.Ordinal);
        order.AddRange(currentOrder.Where(id => !inOrder.Contains(id)));

        // ---- on and off (D5, D3) ----
        var on = new List<string>();
        var off = new List<string>();
        if (options.SwitchOnOff)
            foreach (var (_, m) in matches)
            {
                if (m.Line.Enabled && !m.Package.Enabled) on.Add(m.Package.PackageId);
                else if (!m.Line.Enabled && m.Package.Enabled) off.Add(m.Package.PackageId);
            }
        // core mods count as "not in the list" only when the list has a CORE MODS section (a hand-written list rarely does)
        var listsCore = doc.Sections.Any(s => s.Kind == LoadListSectionKind.Core);
        var notInList = packages.Where(p => !p.IsLayer && p.Enabled && !used.Contains(p.PackageId) && (!p.IsCore || listsCore)).ToList();
        if (options.SwitchOffOthers) off.AddRange(notInList.Select(p => p.PackageId).Where(id => !off.Contains(id)));

        // ---- what the preview says ----
        var kept = Kept(currentOrder, order);
        var nameOf = (string id) => byId.TryGetValue(id, out var p) ? p.Name : id;
        return new LoadListPlan
        {
            Order = order,
            Layout = newLayout,
            SwitchOn = on,
            SwitchOff = off,
            Unlock = unlock,
            Matches = matches.Select(x => x.Match).ToList(),
            Moved = order.Where(id => !kept.Contains(id)).Select(nameOf).ToList(),
            SwitchOnNames = on.Select(nameOf).ToList(),
            SwitchOffNames = off.Select(nameOf).ToList(),
            NewCategories = newCategories,
            CutNames = cutNames,
            CategoryMoves = categoryMoves,
            OtherVersions = matches.Select(x => x.Match).Where(m => m.OtherVersion).ToList(),
            NameOnly = matches.Select(x => x.Match).Where(m => m.Kind == LoadListMatchKind.NameOnly).ToList(),
            Missing = missing,
            Duplicates = duplicates,
            NotInList = notInList.Select(p => p.Name).ToList(),
            StayOutside = stayOutside,
            LockedKept = lockedKept,
            MissingCollections = missingCollections,
            SectionOrderNote = HasUnshownSectionOrder(doc, collectionOf, categoryOf),
            TakesOwnOrder = !orderIsCustom,
            OrderChanged = !order.SequenceEqual(currentOrder),
            LayoutChanged = !SameLayout(layout, newLayout, slugs),
        };
    }

    /// <summary>
    /// Case 16: a category above <c>@mods</c>, or a collection between two parts of MODS. The window always shows
    /// the mods without a category first and the categories after them, all in the one MODS block.
    /// </summary>
    private static bool HasUnshownSectionOrder(LoadListDocument doc, Dictionary<int, LoadListCollection> collectionOf, Dictionary<int, string> categoryOf)
    {
        var sawCategory = false;
        var sawModsPart = false;
        var collectionAfterMods = false;
        for (var si = 0; si < doc.Sections.Count; si++)
        {
            var s = doc.Sections[si];
            var isCategory = categoryOf.ContainsKey(si);
            var isMods = s.Kind == LoadListSectionKind.Mods;
            if (isMods && sawCategory) return true;
            if (isMods || isCategory)
            {
                if (collectionAfterMods) return true;
                sawModsPart = true;
                sawCategory |= isCategory;
            }
            else if (collectionOf.ContainsKey(si) && sawModsPart) collectionAfterMods = true;
        }
        return false;
    }

    private static bool SameLayout(BlockLayout a, BlockLayout b, IReadOnlyList<string> slugs) =>
        a.NormalizedBlocks(slugs).SequenceEqual(b.NormalizedBlocks(slugs))
        && a.Categories.SequenceEqual(b.Categories)
        && a.CategoryOf.Count == b.CategoryOf.Count
        && a.CategoryOf.All(kv => b.CategoryOf.TryGetValue(kv.Key, out var v) && v == kv.Value);

    /// <summary>
    /// <paramref name="current"/> with the items of <paramref name="wanted"/> in the order of <paramref name="wanted"/>:
    /// known items swap into the places they hold, new items go right after the item before them in
    /// <paramref name="wanted"/> (the first one before the next known item). Items not in <paramref name="wanted"/> keep their places.
    /// </summary>
    public static List<string> MergeOrder(IReadOnlyList<string> current, IReadOnlyList<string> wanted)
    {
        var distinct = wanted.Distinct(StringComparer.Ordinal).ToList();
        var currentSet = current.ToHashSet(StringComparer.Ordinal);
        var known = distinct.Where(currentSet.Contains).ToList();
        var knownSet = known.ToHashSet(StringComparer.Ordinal);
        var result = current.ToList();
        var k = 0;
        for (var i = 0; i < result.Count; i++)
            if (knownSet.Contains(result[i])) result[i] = known[k++];
        for (var w = 0; w < distinct.Count; w++)
        {
            var item = distinct[w];
            if (result.Contains(item)) continue;
            int at;
            if (w > 0) at = result.IndexOf(distinct[w - 1]) + 1;
            else
            {
                var next = distinct.Skip(1).FirstOrDefault(result.Contains);
                at = next is null ? result.Count : result.IndexOf(next);
            }
            result.Insert(at, item);
        }
        return result;
    }

    /// <summary>Items that keep their relative order (a longest common subsequence); the rest counts as moved.</summary>
    private static HashSet<string> Kept(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var kept = new HashSet<string>(StringComparer.Ordinal);
        int n = before.Count, m = after.Count;
        if ((long)n * m > 4_000_000)
        {
            // very large lists: count by position only
            for (var i = 0; i < Math.Min(n, m); i++) if (before[i] == after[i]) kept.Add(before[i]);
            return kept;
        }
        var dp = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                dp[i, j] = before[i] == after[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (before[x] == after[y]) { kept.Add(before[x]); x++; y++; }
            else if (dp[x + 1, y] >= dp[x, y + 1]) x++;
            else y++;
        }
        return kept;
    }
}
