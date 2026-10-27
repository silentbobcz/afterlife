using System.Text.Json;
using System.Web;
using Afterlife.Core;

namespace Afterlife.Infrastructure;

// P50/P51: Nexus Mods collections. A collection is its own record in the library; mods are not copied: a package is
// shared by the user's own mods and any number of collections. See AFTERLIFE-souhrn-0.4.md, chapter 4.

/// <summary>A link to a collection: nxm://cyberpunk2077/collections/slug/revisions/3 or a nexusmods.com page.</summary>
public sealed record CollectionLink(string Game, string Slug, int? Revision)
{
    public static bool TryParse(string? text, out CollectionLink link)
    {
        link = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!Uri.TryCreate(text.Trim().Trim('"'), UriKind.Absolute, out var uri)) return false;
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        string? game;
        if (uri.Scheme.Equals("nxm", StringComparison.OrdinalIgnoreCase)) game = uri.Host.ToLowerInvariant();
        else if (uri.Scheme is "https" or "http" && uri.Host.EndsWith("nexusmods.com", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Count > 0 && parts[0].Equals("games", StringComparison.OrdinalIgnoreCase)) parts.RemoveAt(0);
            if (parts.Count == 0) return false;
            game = parts[0].ToLowerInvariant();
            parts.RemoveAt(0);
        }
        else return false;
        var i = parts.FindIndex(p => p.Equals("collections", StringComparison.OrdinalIgnoreCase));
        if (i < 0 || i + 1 >= parts.Count) return false;
        var slug = parts[i + 1];
        if (slug.Length == 0 || !slug.All(c => char.IsLetterOrDigit(c) || c is '-' or '_')) return false;
        int? rev = null;
        if (i + 3 < parts.Count && parts[i + 2].Equals("revisions", StringComparison.OrdinalIgnoreCase) && int.TryParse(parts[i + 3], out var r)) rev = r;
        link = new CollectionLink(game, slug, rev);
        return true;
    }

    public string PageUrl => $"https://www.nexusmods.com/games/{Game}/collections/{Slug}";
}

public enum CollectionSource { Nexus, Bundle, Browse, Direct }

/// <summary>One entry of a collection revision.</summary>
public sealed record CollectionItem(
    string Name,
    string? Version,
    bool Optional,
    CollectionSource Source,
    string? ModId,
    string? FileId,
    string? Md5,
    long? Size,
    string? LogicalFileName,
    string? Url,
    string? Instructions,
    IReadOnlyList<FomodChoice>? Choices,
    int Phase,
    string? UpdatePolicy)
{
    /// <summary>Stable key inside a collection: Nexus file id, else MD5, else name.</summary>
    public string Key => FileId is not null ? "file:" + FileId : Md5 is not null ? "md5:" + Md5.ToLowerInvariant() : "name:" + Name.ToLowerInvariant();
    public string? PageUrl => ModId is null ? Url : $"https://www.nexusmods.com/{NxmLink.Cyberpunk}/mods/{ModId}?tab=files" + (FileId is null ? "" : $"&file_id={FileId}&nmm=1");
}

/// <summary>"A loads before/after B", by item key.</summary>
public sealed record CollectionRule(string Type, string From, string To);

public sealed record CollectionRevisionInfo(
    string Slug,
    string Name,
    string? Curator,
    int Revision,
    int? LatestRevision,
    string? Summary,
    string? Instructions,
    bool Adult,
    long TotalSize,
    string? BundleLink,
    IReadOnlyList<CollectionItem> Items,
    IReadOnlyList<CollectionRule> Rules)
{
    /// <summary>Load order of archive/pc/mod from a modlist.txt shipped by the collection (archive file names).</summary>
    public IReadOnlyList<string> ArchiveOrder { get; init; } = [];
    public bool FromBundle { get; init; }
}

/// <summary>A collection in the library.</summary>
public sealed record CollectionRecord(
    CollectionRevisionInfo Revision,
    string? ProfileId,
    IReadOnlyDictionary<string, string> PackageOf,
    IReadOnlyList<string> ChosenOptional,
    IReadOnlyList<string> PreExisting,
    bool OrderLocked,
    DateTimeOffset AddedAt)
{
    public string Slug => Revision.Slug;
    public string Name => Revision.Name;
    public IEnumerable<CollectionItem> Wanted => Revision.Items.Where(i => !i.Optional || ChosenOptional.Contains(i.Key));
    public IReadOnlyList<CollectionItem> Missing => Wanted.Where(i => !PackageOf.ContainsKey(i.Key)).ToList();
    public string Progress => $"{Wanted.Count(i => PackageOf.ContainsKey(i.Key))}/{Wanted.Count()}";
}

public enum ItemStatus { InLibrary, OtherVersion, Missing, OutsideNexus, Bundled }

public sealed record CollectionItemState(CollectionItem Item, ItemStatus Status, string? PackageId, string Label);

public sealed record RemovalGroup(string PackageId, string Name, string? KeptBecause);

public sealed record RevisionDiff(IReadOnlyList<CollectionItem> Added, IReadOnlyList<(CollectionItem Old, CollectionItem New)> Updated,
    IReadOnlyList<CollectionItem> Removed, int Unchanged);

public static class CollectionStore
{
    private const string Index = "collections";
    private static string Key(string slug) => "collection:" + slug;

    public static IReadOnlyList<CollectionRecord> List(AfterlifeStore store)
    {
        var json = store.GetMeta(Index);
        if (json is null) return [];
        List<string> slugs;
        try { slugs = Json.Deserialize<List<string>>(json) ?? []; } catch (JsonException) { return []; }
        return slugs.Select(s => Load(store, s)).OfType<CollectionRecord>().ToList();
    }

    public static CollectionRecord? Load(AfterlifeStore store, string slug)
    {
        var json = store.GetMeta(Key(slug));
        if (json is null) return null;
        try { return Json.Deserialize<CollectionRecord>(json); } catch (JsonException) { return null; }
    }

    public static void Save(AfterlifeStore store, CollectionRecord r)
    {
        store.SetMeta(Key(r.Slug), Json.Serialize(r));
        var slugs = List(store).Select(x => x.Slug).ToList();
        if (!slugs.Contains(r.Slug)) { slugs.Add(r.Slug); store.SetMeta(Index, Json.Serialize(slugs)); }
    }

    public static void Delete(AfterlifeStore store, string slug)
    {
        store.SetMeta(Key(slug), "");
        store.SetMeta(Index, Json.Serialize(List(store).Select(x => x.Slug).Where(s => s != slug).ToList()));
    }
}

public static class CollectionLogic
{
    /// <summary>Which package in the library is this item: same Nexus file, or same archive (MD5).</summary>
    public static string? Match(CollectionItem item, IReadOnlyList<PackageManifest> packages) =>
        packages.FirstOrDefault(p => p.LayerOfModId is null && item.FileId is not null && p.NexusFileId == item.FileId)?.PackageId
        ?? packages.FirstOrDefault(p => p.LayerOfModId is null && item.Md5 is not null && string.Equals(p.SourceMd5, item.Md5, StringComparison.OrdinalIgnoreCase))?.PackageId;

    public static IReadOnlyList<CollectionItemState> States(CollectionRevisionInfo rev, IReadOnlyList<PackageManifest> packages)
    {
        var list = new List<CollectionItemState>();
        foreach (var i in rev.Items)
        {
            if (Match(i, packages) is { } pid) { list.Add(new(i, ItemStatus.InLibrary, pid, "in library")); continue; }
            if (i.Source == CollectionSource.Bundle) { list.Add(new(i, ItemStatus.Bundled, null, "comes with the collection")); continue; }
            if (i.Source is CollectionSource.Browse or CollectionSource.Direct || i.ModId is null) { list.Add(new(i, ItemStatus.OutsideNexus, null, "outside Nexus · add by hand")); continue; }
            var other = packages.FirstOrDefault(p => p.LayerOfModId is null && p.NexusModId == i.ModId);
            // P81: a core mod already in the library is used as it is (no second version, the core is not touched)
            if (other is not null && other.Provides.Any(c => c.StartsWith("framework:", StringComparison.Ordinal)))
            { list.Add(new(i, ItemStatus.InLibrary, other.PackageId, $"core mod in library ({other.Version}) · kept as it is")); continue; }
            list.Add(other is null ? new(i, ItemStatus.Missing, null, "missing · download")
                : new(i, ItemStatus.OtherVersion, other.PackageId, $"other version in library ({other.Version}) · download {i.Version}"));
        }
        return list;
    }

    /// <summary>
    /// Load order for the collection's packages: modlist.txt of the collection first, then "before/after" rules,
    /// then the collection's own item order. Returns package ids.
    /// </summary>
    public static IReadOnlyList<string> Order(CollectionRecord r, IReadOnlyList<PackageManifest> packages)
    {
        var byKey = r.PackageOf;
        var items = r.Wanted.Where(i => byKey.ContainsKey(i.Key)).ToList();
        var pos = new Dictionary<string, int>();
        for (var i = 0; i < items.Count; i++) pos[items[i].Key] = i * 10;
        // modlist.txt: rank by the first archive of each package
        if (r.Revision.ArchiveOrder.Count > 0)
        {
            var rank = r.Revision.ArchiveOrder.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i, StringComparer.OrdinalIgnoreCase);
            foreach (var it in items)
            {
                var p = packages.FirstOrDefault(x => x.PackageId == byKey[it.Key]);
                var best = p?.Files.Select(f => GamePath.Parse(f.Target)).Where(g => string.Equals(g.ParentValue, "archive/pc/mod", StringComparison.OrdinalIgnoreCase))
                    .Select(g => rank.TryGetValue(g.FileName, out var k) ? k : int.MaxValue).DefaultIfEmpty(int.MaxValue).Min() ?? int.MaxValue;
                if (best != int.MaxValue) pos[it.Key] = -100000 + best;
            }
        }
        var ordered = items.OrderBy(i => pos[i.Key]).Select(i => i.Key).ToList();
        // before/after rules: stable topological pass
        var rules = r.Revision.Rules.Where(x => ordered.Contains(x.From) && ordered.Contains(x.To)).ToList();
        for (var pass = 0; pass < 3 && rules.Count > 0; pass++)
            foreach (var rule in rules)
            {
                var (first, second) = rule.Type == "before" ? (rule.From, rule.To) : (rule.To, rule.From);
                var a = ordered.IndexOf(first);
                var b = ordered.IndexOf(second);
                if (a > b) { ordered.RemoveAt(a); ordered.Insert(b, first); }
            }
        return ordered.Select(k => byKey[k]).Distinct().ToList();
    }

    /// <summary>What removing a collection would do: packages that go, and those that stay with the reason.</summary>
    public static (IReadOnlyList<RemovalGroup> Remove, IReadOnlyList<RemovalGroup> Keep) RemovalPlan(
        CollectionRecord r, IReadOnlyList<CollectionRecord> all, InstallOverview o)
    {
        var remove = new List<RemovalGroup>();
        var keep = new List<RemovalGroup>();
        var mine = r.PackageOf.Values.Distinct().ToList();
        var leaving = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pid in mine)
        {
            var m = o.Mods.FirstOrDefault(x => x.Package.PackageId == pid);
            if (m is null) continue;
            var other = all.FirstOrDefault(c => c.Slug != r.Slug && c.PackageOf.Values.Contains(pid));
            string? why = other is not null ? $"also in collection {other.Name}"
                : r.PreExisting.Contains(pid) ? "added by you before the collection"
                : null;
            if (why is null) leaving.Add(pid); else keep.Add(new RemovalGroup(pid, m.Package.ModName, why));
        }
        // needed by a switched-on mod that stays (any profile's requirement from files or the Nexus page)
        var staying = o.Mods.Where(m => !leaving.Contains(m.Package.PackageId)).ToList();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var pid in leaving.ToList())
            {
                var p = o.Mods.First(x => x.Package.PackageId == pid).Package;
                var needer = staying.FirstOrDefault(s => s.Requirements.Any(q => q.ProviderPackageId == pid)
                    || s.Package.Requirements.Any(q => q.Mandatory && p.Provides.Contains(q.Capability) && !staying.Any(x => x.Package.PackageId != s.Package.PackageId && x.Package.Provides.Contains(q.Capability))));
                if (needer is null) continue;
                leaving.Remove(pid);
                var kept = o.Mods.First(x => x.Package.PackageId == pid);
                staying.Add(kept);
                keep.Add(new RemovalGroup(pid, p.ModName, $"required by {needer.Package.ModName} outside this collection"));
                changed = true;
            }
        }
        foreach (var pid in leaving) remove.Add(new RemovalGroup(pid, o.Mods.First(x => x.Package.PackageId == pid).Package.ModName, null));
        return (remove, keep);
    }

    public static RevisionDiff Diff(CollectionRevisionInfo oldRev, CollectionRevisionInfo newRev)
    {
        string Mod(CollectionItem i) => i.ModId is not null ? "mod:" + i.ModId : i.Key;
        var oldBy = oldRev.Items.GroupBy(Mod).ToDictionary(g => g.Key, g => g.First());
        var newBy = newRev.Items.GroupBy(Mod).ToDictionary(g => g.Key, g => g.First());
        var added = newBy.Where(kv => !oldBy.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList();
        var removed = oldBy.Where(kv => !newBy.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList();
        var updated = newBy.Where(kv => oldBy.TryGetValue(kv.Key, out var o) && o.Key != kv.Value.Key).Select(kv => (oldBy[kv.Key], kv.Value)).ToList();
        var unchanged = newBy.Count(kv => oldBy.TryGetValue(kv.Key, out var o) && o.Key == kv.Value.Key);
        return new RevisionDiff(added, updated, removed, unchanged);
    }

    /// <summary>Reads Vortex's collection.json (inside the collection bundle). Unknown parts are ignored.</summary>
    public static CollectionRevisionInfo FromManifest(CollectionRevisionInfo api, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        static string? S(JsonElement e, string n) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out var v)
            ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.ToString(), _ => null } : null;
        static bool B(JsonElement e, string n) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.True;
        var items = new List<CollectionItem>();
        var keyOfRef = new List<(string? Md5, string? Logical, string? FileId, string Key)>();
        if (root.TryGetProperty("mods", out var mods) && mods.ValueKind == JsonValueKind.Array)
            foreach (var m in mods.EnumerateArray())
            {
                var src = m.TryGetProperty("source", out var s) ? s : default;
                var type = S(src, "type")?.ToLowerInvariant() switch
                {
                    "bundle" => CollectionSource.Bundle, "browse" => CollectionSource.Browse, "direct" => CollectionSource.Direct, _ => CollectionSource.Nexus,
                };
                var item = new CollectionItem(S(m, "name") ?? S(src, "logicalFilename") ?? "mod", S(m, "version"), B(m, "optional"), type,
                    S(src, "modId"), S(src, "fileId"), S(src, "md5"), long.TryParse(S(src, "fileSize"), out var size) ? size : null,
                    S(src, "logicalFilename"), S(src, "url"), S(m, "instructions") ?? S(src, "instructions"),
                    ParseChoices(m), int.TryParse(S(m, "phase"), out var ph) ? ph : 0, S(src, "updatePolicy"));
                items.Add(item);
                keyOfRef.Add((item.Md5, item.LogicalFileName, item.FileId, item.Key));
            }
        string? Resolve(JsonElement r)
        {
            var md5 = S(r, "fileMD5");
            var logical = S(r, "logicalFileName");
            var hit = keyOfRef.FirstOrDefault(k => (md5 is not null && string.Equals(k.Md5, md5, StringComparison.OrdinalIgnoreCase))
                || (logical is not null && string.Equals(k.Logical, logical, StringComparison.OrdinalIgnoreCase)));
            return hit.Key;
        }
        var rules = new List<CollectionRule>();
        if (root.TryGetProperty("modRules", out var mr) && mr.ValueKind == JsonValueKind.Array)
            foreach (var r in mr.EnumerateArray())
            {
                var t = S(r, "type");
                if (t is not ("before" or "after")) continue;
                var from = r.TryGetProperty("source", out var rs) ? Resolve(rs) : null;
                var to = r.TryGetProperty("reference", out var rf) ? Resolve(rf) : null;
                if (from is not null && to is not null && from != to) rules.Add(new CollectionRule(t, from, to));
            }
        var info = root.TryGetProperty("info", out var inf) ? inf : default;
        // prefer the API list (exact optional flags), enrich with manifest details
        var merged = items.Count == 0 ? api.Items : items.Select(i =>
        {
            var a = api.Items.FirstOrDefault(x => x.FileId is not null && x.FileId == i.FileId);
            return a is null ? i : i with { Optional = a.Optional, ModId = i.ModId ?? a.ModId, Version = i.Version ?? a.Version };
        }).ToList();
        return api with
        {
            Items = merged,
            Rules = rules,
            Instructions = api.Instructions ?? S(info, "installInstructions"),
            FromBundle = true,
        };
    }

    private static IReadOnlyList<FomodChoice>? ParseChoices(JsonElement mod)
    {
        if (!mod.TryGetProperty("choices", out var c) || c.ValueKind != JsonValueKind.Object) return null;
        if (!c.TryGetProperty("options", out var steps) || steps.ValueKind != JsonValueKind.Array) return null;
        var list = new List<FomodChoice>();
        foreach (var step in steps.EnumerateArray())
        {
            var stepName = step.TryGetProperty("name", out var sn) ? sn.GetString() ?? "" : "";
            if (!step.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array) continue;
            foreach (var g in groups.EnumerateArray())
            {
                var groupName = g.TryGetProperty("name", out var gn) ? gn.GetString() ?? "" : "";
                var picks = g.TryGetProperty("choices", out var ch) && ch.ValueKind == JsonValueKind.Array
                    ? ch.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "").Where(x => x.Length > 0).ToList() : [];
                list.Add(new FomodChoice(stepName, groupName, picks));
            }
        }
        return list;
    }

    /// <summary>The revision as the GraphQL API lists it (no installer choices or rules; those come from the bundle).</summary>
    public static CollectionRevisionInfo FromGraphQl(string slug, JsonElement root)
    {
        if (!root.TryGetProperty("data", out var d) || !d.TryGetProperty("collectionRevision", out var r) || r.ValueKind != JsonValueKind.Object)
        {
            var msg = root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0
                && errs[0].TryGetProperty("message", out var m) ? m.GetString() : null;
            throw new NexusException(msg is null ? "Nexus Mods does not know this collection" : "Nexus Mods: " + msg);
        }
        static string? S(JsonElement e, string n) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out var v)
            ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.ToString(), _ => null } : null;
        var col = r.TryGetProperty("collection", out var c) ? c : default;
        var user = col.ValueKind == JsonValueKind.Object && col.TryGetProperty("user", out var u) ? u : default;
        var latest = col.ValueKind == JsonValueKind.Object && col.TryGetProperty("latestPublishedRevision", out var lr) && lr.ValueKind == JsonValueKind.Object
            && int.TryParse(S(lr, "revisionNumber"), out var ln) ? ln : (int?)null;
        var items = new List<CollectionItem>();
        if (r.TryGetProperty("modFiles", out var files) && files.ValueKind == JsonValueKind.Array)
            foreach (var f in files.EnumerateArray())
            {
                var file = f.TryGetProperty("file", out var fe) ? fe : default;
                var mod = file.ValueKind == JsonValueKind.Object && file.TryGetProperty("mod", out var me) ? me : default;
                var name = S(mod, "name") ?? S(file, "name") ?? "mod";
                items.Add(new CollectionItem(name, S(f, "version") ?? S(file, "version"), f.TryGetProperty("optional", out var o) && o.ValueKind == JsonValueKind.True,
                    CollectionSource.Nexus, S(file, "modId"), S(f, "fileId"), null, long.TryParse(S(file, "sizeInBytes"), out var sz) ? sz : null,
                    S(file, "name"), null, null, null, 0, S(f, "updatePolicy")));
            }
        if (r.TryGetProperty("externalResources", out var ext) && ext.ValueKind == JsonValueKind.Array)
            foreach (var e in ext.EnumerateArray())
                items.Add(new CollectionItem(S(e, "name") ?? "external", S(e, "version"), e.TryGetProperty("optional", out var eo) && eo.ValueKind == JsonValueKind.True,
                    CollectionSource.Browse, null, null, null, null, null, S(e, "resourceUrl"), S(e, "instructions"), null, 0, null));
        return new CollectionRevisionInfo(slug, S(col, "name") ?? slug, S(user, "name"), int.TryParse(S(r, "revisionNumber"), out var rn) ? rn : 0, latest,
            S(col, "summary"), S(r, "installationInfo"), r.TryGetProperty("adultContent", out var ad) && ad.ValueKind == JsonValueKind.True,
            long.TryParse(S(r, "totalSize"), out var ts) ? ts : 0, S(r, "downloadLink"), items, []);
    }

    public const string RevisionQuery = """
        query($slug: String, $revision: Int, $domain: String) {
          collectionRevision(slug: $slug, revision: $revision, domainName: $domain, viewAdultContent: true) {
            revisionNumber downloadLink installationInfo adultContent totalSize
            collection { name summary slug user { name } latestPublishedRevision { revisionNumber } }
            modFiles { fileId optional version updatePolicy file { fileId name version modId sizeInBytes mod { name } } }
            externalResources { name version optional resourceUrl instructions }
          }
        }
        """;
}

public static class CollectionLinks
{
    /// <summary>Download links in the answer of a collection "downloadLink" (several shapes are accepted).</summary>
    public static IReadOnlyList<Uri> Parse(JsonElement root)
    {
        var list = new List<Uri>();
        void Walk(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject())
                    {
                        if (p.Name.Equals("URI", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String
                            && Uri.TryCreate(p.Value.GetString(), UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps) list.Add(u);
                        else Walk(p.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var x in e.EnumerateArray()) Walk(x);
                    break;
            }
        }
        Walk(root);
        return list;
    }

    public static string? QueryValue(string url, string name) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? HttpUtility.ParseQueryString(u.Query)[name] : null;
}
