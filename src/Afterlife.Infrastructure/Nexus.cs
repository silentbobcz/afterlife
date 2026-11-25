using Afterlife.Core;
using System.Text.Json;
using System.Web;

namespace Afterlife.Infrastructure;

/// <summary>
/// A "Mod Manager Download" link from nexusmods.com, for example
/// <c>nxm://cyberpunk2077/mods/107/files/12345?key=abc&amp;expires=1700000000&amp;user_id=1</c>.
/// Free accounts can only download through such links (the key and expiry are issued by the website);
/// Premium accounts may also ask the API for a link without them.
/// </summary>
public sealed record NxmLink(string Game, string ModId, string FileId, string? Key, string? Expires)
{
    public const string Cyberpunk = "cyberpunk2077";

    public static bool TryParse(string? text, out NxmLink link)
    {
        link = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!Uri.TryCreate(text.Trim().Trim('"'), UriKind.Absolute, out var uri) || !uri.Scheme.Equals("nxm", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 4 || !parts[0].Equals("mods", StringComparison.OrdinalIgnoreCase) || !parts[2].Equals("files", StringComparison.OrdinalIgnoreCase)) return false;
        if (!parts[1].All(char.IsDigit) || !parts[3].All(char.IsDigit) || parts[1].Length == 0 || parts[3].Length == 0) return false;
        var q = HttpUtility.ParseQueryString(uri.Query);
        link = new NxmLink(uri.Host.ToLowerInvariant(), parts[1], parts[3], q["key"], q["expires"]);
        return true;
    }

    public bool IsCyberpunk => Game == Cyberpunk;
    public string ModPage => $"https://www.nexusmods.com/{Game}/mods/{ModId}";
}

public sealed record NexusAccount(string Name, bool IsPremium, bool IsSupporter)
{
    public string Tier => IsPremium ? "Premium" : IsSupporter ? "Supporter" : "Free";
}

public sealed record NexusFileInfo(string FileName, string? Version, string? ModName, string? ModVersion);

public sealed class NexusException(string message) : Exception(message)
{
    /// <summary>Nexus Mods answered 429: no further request gets through until its limit resets.</summary>
    public bool RateLimited { get; init; }
    /// <summary>Nexus Mods answered 404: it does not know what was asked for.</summary>
    public bool NotFound { get; init; }
    /// <summary>The OAuth login was revoked or expired for good: the user has to log in again.</summary>
    public bool SignedOut { get; init; }
}

/// <summary>A mod the Nexus page lists as required.</summary>
public sealed record NexusRequirement(string ModId, string Name, string Url, bool External, string? Notes);

/// <summary>What the Nexus page says about a mod, cached in the library database.</summary>
public sealed record NexusModInfo(
    string ModId,
    string Name,
    string? Summary,
    string? Author,
    string? PictureUrl,
    long Endorsements,
    long Downloads,
    string? Version,
    IReadOnlyList<NexusRequirement> Requirements,
    DateTimeOffset FetchedAt)
{
    public string Url => $"https://www.nexusmods.com/{NxmLink.Cyberpunk}/mods/{ModId}";
}

/// <summary>One download from Nexus Mods through AFTERLIFE (kept even after the mod is deleted).</summary>
public sealed record NexusDownload(string ModId, string FileId, string Name, string? Version, string FileName, DateTimeOffset DownloadedAt)
{
    public string Url => $"https://www.nexusmods.com/{NxmLink.Cyberpunk}/mods/{ModId}";
}

public sealed record NexusFile(string FileId, string Name, string? Version, string Category, long UploadedAt, string? FileName)
{
    public bool IsArchived => Category.Equals("ARCHIVED", StringComparison.OrdinalIgnoreCase) || Category.Equals("DELETED", StringComparison.OrdinalIgnoreCase)
        || Category.Equals("REMOVED", StringComparison.OrdinalIgnoreCase);
}

public sealed record NexusFiles(string ModId, IReadOnlyList<NexusFile> Files, IReadOnlyList<(string Old, string New)> Updates);

/// <summary>A newer file for a package. Exact = the author marked our file as replaced; otherwise the version of the main file differs.</summary>
public sealed record NexusUpdate(string ModId, string FileId, string? Version, string Name, bool Exact, DateTimeOffset CheckedAt)
{
    /// <summary>
    /// Page of the new file. <c>nmm=1</c> opens Nexus' download page for mod managers, where one click on
    /// "Slow download" (free) or "Download" (Premium) sends the file through the nxm:// link straight into AFTERLIFE.
    /// </summary>
    public string Url => $"https://www.nexusmods.com/{NxmLink.Cyberpunk}/mods/{ModId}?tab=files&file_id={FileId}&nmm=1";
    /// <summary>Plain files tab (no mod-manager hand-off), for the case that no nxm:// handler is registered.</summary>
    public string FilesUrl => $"https://www.nexusmods.com/{NxmLink.Cyberpunk}/mods/{ModId}?tab=files&file_id={FileId}";
}

public static class NexusUpdates
{
    private static string Key(string packageId) => "nexus:update:" + packageId;
    private const string None = "none";

    /// <summary>Follows "file X was replaced by file Y" from our file; without a file id compares the version of the newest main file.</summary>
    public static NexusUpdate? Find(NexusFiles files, string? currentFileId, string? currentVersion, DateTimeOffset now)
    {
        if (currentFileId is not null && files.Files.Any(f => f.FileId == currentFileId))
        {
            var id = currentFileId;
            var seen = new HashSet<string> { id };
            while (files.Updates.FirstOrDefault(u => u.Old == id) is { New: { } next } && seen.Add(next)) id = next;
            if (id == currentFileId) return null;
            var f = files.Files.FirstOrDefault(x => x.FileId == id);
            return f is null || f.IsArchived ? null : new NexusUpdate(files.ModId, f.FileId, f.Version, f.Name, true, now);
        }
        var main = files.Files.Where(f => f.Category.Equals("MAIN", StringComparison.OrdinalIgnoreCase)).OrderByDescending(f => f.UploadedAt).FirstOrDefault();
        if (main?.Version is null || currentVersion is null) return null;
        return Norm(main.Version) == Norm(currentVersion) ? null : new NexusUpdate(files.ModId, main.FileId, main.Version, main.Name, false, now);
    }

    private static string Norm(string v)
    {
        var x = v.Trim().TrimStart('v', 'V').ToLowerInvariant();
        while (x.EndsWith(".0", StringComparison.Ordinal)) x = x[..^2];
        return x;
    }

    /// <summary>When the package was last checked (with or without a result).</summary>
    public static DateTimeOffset? CheckedAt(AfterlifeStore store, string packageId) =>
        store.GetMeta(Key(packageId)) is { } v ? CheckedAtOf(v) : null;

    /// <summary>When every package was last checked, by package id, read in one query.</summary>
    public static Dictionary<string, DateTimeOffset> CheckedAll(AfterlifeStore store)
    {
        var prefix = Key("");
        var result = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var (key, value) in store.GetMetaByPrefix(prefix))
            if (CheckedAtOf(value) is { } at) result[key[prefix.Length..]] = at;
        return result;
    }

    private static DateTimeOffset? CheckedAtOf(string stored)
    {
        if (!stored.StartsWith(None, StringComparison.Ordinal)) return Parse(stored)?.CheckedAt;
        return stored.Length > None.Length + 1 && long.TryParse(stored[(None.Length + 1)..], out var t) ? DateTimeOffset.FromUnixTimeSeconds(t) : null;
    }

    /// <summary>The smallest period of the "updated mods" feed that covers every check since <paramref name="oldest"/>; null = too old, read the files.</summary>
    public static string? FeedPeriod(DateTimeOffset oldest, DateTimeOffset now)
    {
        var age = now - oldest;
        return age < TimeSpan.FromHours(23) ? "1d" : age < TimeSpan.FromDays(6.5) ? "1w" : age < TimeSpan.FromDays(28) ? "1m" : null;
    }

    /// <summary>
    /// Packages whose files must be read: never checked, older than the feed, or their mod changed since the last check
    /// (10 minutes of slack for the feed cache). The rest is up to date without another request.
    /// </summary>
    public static IReadOnlyList<string> DueAfterFeed(IEnumerable<(string PackageId, string ModId, DateTimeOffset? CheckedAt)> items,
        IReadOnlyDictionary<string, long>? feed, DateTimeOffset now) =>
        items.Where(i => i.CheckedAt is not { } at || feed is null || FeedPeriod(at, now) is null
                || (feed.TryGetValue(i.ModId, out var t) && DateTimeOffset.FromUnixTimeSeconds(t) >= at - TimeSpan.FromMinutes(10)))
            .Select(i => i.PackageId).ToList();

    /// <summary>Checked now without reading the files: keeps an update found earlier.</summary>
    public static void MarkChecked(AfterlifeStore store, string packageId, DateTimeOffset now)
    {
        var u = Load(store, packageId);
        Save(store, packageId, u is null ? null : u with { CheckedAt = now }, now);
    }

    public static void Save(AfterlifeStore store, string packageId, NexusUpdate? u, DateTimeOffset now) =>
        store.SetMeta(Key(packageId), u is null ? None + "|" + now.ToUnixTimeSeconds() : Json.Serialize(u));

    public static NexusUpdate? Load(AfterlifeStore store, string packageId) => Parse(store.GetMeta(Key(packageId)));

    /// <summary>Every stored update by package id, read in one query.</summary>
    public static Dictionary<string, NexusUpdate> LoadAll(AfterlifeStore store)
    {
        var prefix = Key("");
        var result = new Dictionary<string, NexusUpdate>(StringComparer.Ordinal);
        foreach (var (key, value) in store.GetMetaByPrefix(prefix))
            if (Parse(value) is { } u) result[key[prefix.Length..]] = u;
        return result;
    }

    private static NexusUpdate? Parse(string? v)
    {
        if (v is null || v.StartsWith(None, StringComparison.Ordinal)) return null;
        try { return Json.Deserialize<NexusUpdate>(v); } catch (JsonException) { return null; }
    }

    /// <summary>Checked in the last 24 hours (with or without a result).</summary>
    public static bool IsFresh(AfterlifeStore store, string packageId, DateTimeOffset now)
    {
        var v = store.GetMeta(Key(packageId));
        if (v is null) return false;
        if (v.StartsWith(None, StringComparison.Ordinal))
            return long.TryParse(v[(None.Length + 1)..], out var t) && now - DateTimeOffset.FromUnixTimeSeconds(t) < TimeSpan.FromHours(24);
        return Load(store, packageId) is { } u && now - u.CheckedAt < TimeSpan.FromHours(24);
    }
}

public static class DownloadHistory
{
    private const string Key = "nexus:downloads";
    public const string WebPage = "https://www.nexusmods.com/users/myaccount?tab=download+history";

    public static IReadOnlyList<NexusDownload> List(AfterlifeStore store)
    {
        var json = store.GetMeta(Key);
        if (json is null) return [];
        try { return Json.Deserialize<List<NexusDownload>>(json) ?? []; } catch (JsonException) { return []; }
    }

    public static void Record(AfterlifeStore store, NexusDownload d)
    {
        var list = List(store).Where(x => !(x.ModId == d.ModId && x.FileId == d.FileId)).ToList();
        list.Insert(0, d);
        store.SetMeta(Key, Json.Serialize(list.Take(500).ToList()));
    }
}

public static class NexusInfo
{
    /// <summary>
    /// Nexus has no optional flag on requirements; the author's note says it, at the start ("Optional: …") or
    /// anywhere as a word ("v0.9.0+ (optional, only required for JSON)").
    /// </summary>
    public static bool IsOptionalNote(string? notes) => notes is { } n &&
        (n.TrimStart().StartsWith("recommended", StringComparison.OrdinalIgnoreCase)
         || System.Text.RegularExpressions.Regex.IsMatch(n, @"\boptional(ly)?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

    private static string Key(string modId) => "nexus:mod:" + modId;

    public static NexusModInfo? Load(AfterlifeStore store, string? modId) => modId is null ? null : Parse(store.GetMeta(Key(modId)));

    /// <summary>Every cached Nexus page by mod id, read in one query.</summary>
    public static Dictionary<string, NexusModInfo> LoadAll(AfterlifeStore store)
    {
        var prefix = Key("");
        var result = new Dictionary<string, NexusModInfo>(StringComparer.Ordinal);
        foreach (var (key, value) in store.GetMetaByPrefix(prefix))
            if (Parse(value) is { } info) result[key[prefix.Length..]] = info;
        return result;
    }

    private static NexusModInfo? Parse(string? json)
    {
        if (json is null) return null;
        try { return Json.Deserialize<NexusModInfo>(json); } catch (JsonException) { return null; }
    }

    public static void Save(AfterlifeStore store, NexusModInfo info) => store.SetMeta(Key(info.ModId), Json.Serialize(info));

    /// <summary>Nexus id of a package: confirmed (downloaded/linked), from the core catalog, or guessed from the file name.</summary>
    public static (string? Id, bool Confirmed) IdOf(PackageManifest p)
    {
        if (p.NexusModId is not null && p.Origin == PackageOrigin.Nexus) return (p.NexusModId, true);
        var catalog = p.Provides.Select(c => FrameworkCatalog.Find(c)?.NexusModId).FirstOrDefault(i => i is not null);
        if (catalog is not null) return (catalog, true);
        return (p.NexusModId, false);
    }
}

/// <summary>
/// Minimal Nexus Mods API v1 client: who owns the key (Premium or not), mod and file names, download links.
/// The credential is the user's OAuth login (<see cref="NexusTokens"/>) or, in test builds, a personal API key;
/// it never leaves this machine except to Nexus Mods.
/// </summary>
public sealed class NexusClient : IDisposable
{
    public const string ApiBase = "https://api.nexusmods.com/v1/";
    public const string KeyPage = "https://www.nexusmods.com/settings/api-keys";
    public const string CyberpunkPage = "https://www.nexusmods.com/games/cyberpunk2077/mods";
    public const string GraphQl = "https://api.nexusmods.com/v2/graphql";
    public const string CyberpunkGameId = "3333";

    private readonly string _appVersion;
    private string _apiKey;
    private readonly HttpMessageHandler? _handler;
    private readonly HttpClient? _own; // only with an injected handler; otherwise the shared client of NexusHttp
    private HttpClient Http => _own ?? NexusHttp.Api;

    public NexusClient(string credential, string appVersion, HttpMessageHandler? handler = null)
    {
        (_apiKey, _appVersion, _handler) = (credential, appVersion, handler);
        _own = handler is null ? null : NexusHttp.WithHandler(handler);
    }

    /// <summary>The credential for the next request; an OAuth access token is refreshed first when it is about to expire.</summary>
    private async Task<string> CredentialAsync(CancellationToken ct)
    {
        if (NexusTokens.From(_apiKey) is not { } tokens) return _apiKey;
        var fresh = await NexusOAuth.FreshAsync(tokens, ct, _handler);
        if (fresh != tokens) _apiKey = fresh.Serialize();
        return _apiKey;
    }

    public async Task<NexusAccount> ValidateAsync(CancellationToken ct = default)
    {
        using var doc = await GetJsonAsync("users/validate.json", ct);
        return ParseAccount(doc.RootElement);
    }

    /// <summary>Accepts both "is_premium" and the older "is_premium?" spelling.</summary>
    public static NexusAccount ParseAccount(JsonElement root)
    {
        static bool Flag(JsonElement e, string name) =>
            (e.TryGetProperty(name, out var v) || e.TryGetProperty(name + "?", out v)) && v.ValueKind == JsonValueKind.True;
        var name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : "Nexus user";
        return new NexusAccount(name, Flag(root, "is_premium"), Flag(root, "is_supporter"));
    }

    public async Task<NexusFileInfo> FileInfoAsync(NxmLink link, CancellationToken ct = default)
    {
        string fileName = $"nexus-{link.ModId}-{link.FileId}.zip";
        string? version = null, modName = null, modVersion = null;
        using (var f = await GetJsonAsync($"games/{link.Game}/mods/{link.ModId}/files/{link.FileId}.json", ct))
        {
            var r = f.RootElement;
            if (r.TryGetProperty("file_name", out var fn) && fn.ValueKind == JsonValueKind.String) fileName = fn.GetString()!;
            if (r.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String) version = v.GetString();
        }
        try
        {
            using var m = await GetJsonAsync($"games/{link.Game}/mods/{link.ModId}.json", ct);
            var r = m.RootElement;
            if (r.TryGetProperty("name", out var mn) && mn.ValueKind == JsonValueKind.String) modName = mn.GetString();
            if (r.TryGetProperty("version", out var mv) && mv.ValueKind == JsonValueKind.String) modVersion = mv.GetString();
        }
        catch (NexusException) { /* name is optional; the file name is enough */ }
        return new NexusFileInfo(SafeFileName(fileName), version, modName, modVersion);
    }

    /// <summary>
    /// Summary, author, picture and counts (API v1) plus the requirements listed on the mod page (GraphQL v2).
    /// Requirements are best effort: when GraphQL fails the rest is still returned.
    /// </summary>
    /// <summary>One revision of a collection (latest when no number).</summary>
    public async Task<CollectionRevisionInfo> CollectionRevisionAsync(CollectionLink link, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { query = CollectionLogic.RevisionQuery, variables = new { slug = link.Slug, revision = link.Revision, domain = link.Game } });
        using var req = NexusHttp.ApiRequest(HttpMethod.Post, GraphQl, await CredentialAsync(ct), _appVersion, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        HttpResponseMessage resp;
        try { resp = await Http.SendAsync(req, ct); }
        catch (HttpRequestException ex) { throw new NexusException("Nexus Mods is not reachable: " + ex.Message); }
        using (resp)
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new NexusException($"Nexus Mods answered {(int)resp.StatusCode}");
            using var doc = JsonDocument.Parse(text);
            return CollectionLogic.FromGraphQl(link.Slug, doc.RootElement);
        }
    }

    /// <summary>Downloads the collection bundle (.7z with collection.json and bundled files) into the given file.</summary>
    public async Task<bool> CollectionBundleAsync(string downloadLink, string destination, CancellationToken ct = default)
    {
        var path = downloadLink.StartsWith("/v1/", StringComparison.Ordinal) ? downloadLink[4..] : downloadLink.TrimStart('/');
        JsonDocument doc;
        try { doc = await GetJsonAsync(path, ct); }
        catch (NexusException) { return false; }
        using (doc)
        {
            var links = CollectionLinks.Parse(doc.RootElement);
            if (links.Count == 0) return false;
            await DownloadAsync(links[0], destination, null, ct);
            return true;
        }
    }

    /// <summary>Files of a mod and which file replaced which.</summary>
    public async Task<NexusFiles> FilesAsync(string modId, CancellationToken ct = default)
    {
        using var doc = await GetJsonAsync($"games/{NxmLink.Cyberpunk}/mods/{modId}/files.json", ct);
        return ParseFiles(modId, doc.RootElement);
    }

    public static NexusFiles ParseFiles(string modId, JsonElement root)
    {
        static string? Str(JsonElement e, string n) => e.TryGetProperty(n, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.ToString(), _ => null,
        } : null;
        static long Num(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var x) ? x : 0;
        var files = new List<NexusFile>();
        if (root.TryGetProperty("files", out var fs) && fs.ValueKind == JsonValueKind.Array)
            foreach (var f in fs.EnumerateArray())
            {
                var id = Str(f, "file_id") ?? (f.TryGetProperty("id", out var ida) && ida.ValueKind == JsonValueKind.Array && ida.GetArrayLength() > 0 ? ida[0].ToString() : null);
                if (id is null) continue;
                files.Add(new NexusFile(id, Str(f, "name") ?? "", Str(f, "version"), Str(f, "category_name") ?? "", Num(f, "uploaded_timestamp"), Str(f, "file_name")));
            }
        var updates = new List<(string Old, string New)>();
        if (root.TryGetProperty("file_updates", out var us) && us.ValueKind == JsonValueKind.Array)
            foreach (var u in us.EnumerateArray())
                if (Str(u, "old_file_id") is { } o && Str(u, "new_file_id") is { } n) updates.Add((o, n));
        return new NexusFiles(modId, files, updates);
    }

    /// <summary>
    /// Files on Nexus Mods with exactly this content (MD5 of the downloaded archive). Empty when Nexus knows none.
    /// </summary>
    public async Task<IReadOnlyList<NexusMd5Match>> Md5SearchAsync(string md5, CancellationToken ct = default)
    {
        try
        {
            using var doc = await GetJsonAsync($"games/{NxmLink.Cyberpunk}/mods/md5_search/{md5.ToLowerInvariant()}.json", ct);
            return ParseMd5Search(doc.RootElement);
        }
        catch (NexusException ex) when (ex.NotFound) { return []; }
    }

    public static IReadOnlyList<NexusMd5Match> ParseMd5Search(JsonElement root)
    {
        static string? Str(JsonElement e, string n) => e.TryGetProperty(n, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.ToString(), _ => null,
        } : null;
        var list = new List<NexusMd5Match>();
        if (root.ValueKind != JsonValueKind.Array) return list;
        foreach (var e in root.EnumerateArray())
        {
            if (!e.TryGetProperty("mod", out var m) || m.ValueKind != JsonValueKind.Object) continue;
            if (!e.TryGetProperty("file_details", out var f) || f.ValueKind != JsonValueKind.Object) continue;
            // the search is asked per game, but an answer for another game must never link a Cyberpunk mod
            if (Str(m, "domain_name") is { } game && !game.Equals(NxmLink.Cyberpunk, StringComparison.OrdinalIgnoreCase)) continue;
            if (Str(m, "mod_id") is not { Length: > 0 } modId || !modId.All(char.IsDigit)) continue;
            if (Str(f, "file_id") is not { Length: > 0 } fileId || !fileId.All(char.IsDigit)) continue;
            list.Add(new NexusMd5Match(modId, Str(m, "name"), fileId, Str(f, "file_name"), Str(f, "version")));
        }
        return list;
    }

    /// <summary>Mods of the game whose files changed in the period (1d, 1w, 1m) with the time of the last file change. One request for all mods.</summary>
    public async Task<IReadOnlyDictionary<string, long>> UpdatedAsync(string period, CancellationToken ct = default)
    {
        using var doc = await GetJsonAsync($"games/{NxmLink.Cyberpunk}/mods/updated.json?period={period}", ct);
        return ParseUpdated(doc.RootElement);
    }

    public static IReadOnlyDictionary<string, long> ParseUpdated(JsonElement root)
    {
        var map = new Dictionary<string, long>();
        if (root.ValueKind != JsonValueKind.Array) return map;
        foreach (var e in root.EnumerateArray())
        {
            if (!e.TryGetProperty("mod_id", out var id)) continue;
            long t = 0;
            foreach (var n in new[] { "latest_file_update", "latest_mod_activity" })
                if (e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var x)) t = Math.Max(t, x);
            map[id.ToString()] = t;
        }
        return map;
    }

    public async Task<NexusModInfo> ModInfoAsync(string modId, CancellationToken ct = default)
    {
        using var doc = await GetJsonAsync($"games/{NxmLink.Cyberpunk}/mods/{modId}.json", ct);
        var r = doc.RootElement;
        static string? Str(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        static long Num(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var x) ? x : 0;
        var reqs = new List<NexusRequirement>();
        try { reqs.AddRange(await RequirementsAsync(modId, ct)); }
        catch (Exception ex) when (ex is NexusException or HttpRequestException or JsonException or TaskCanceledException) { }
        var summary = Str(r, "summary");
        return new NexusModInfo(modId, Str(r, "name") ?? $"Nexus mod {modId}", summary is null ? null : System.Net.WebUtility.HtmlDecode(summary).Replace("<br />", " ").Trim(),
            Str(r, "author"), Str(r, "picture_url"), Num(r, "endorsement_count"), Num(r, "mod_downloads"), Str(r, "version"), reqs, DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyList<NexusRequirement>> RequirementsAsync(string modId, CancellationToken ct)
    {
        const string query = "query($modId: ID!, $gameId: ID!) { mod(modId: $modId, gameId: $gameId) { modRequirements { nexusRequirements { nodes { modId modName url externalRequirement notes } } } } }";
        var body = JsonSerializer.Serialize(new { query, variables = new { modId, gameId = CyberpunkGameId } });
        using var req = NexusHttp.ApiRequest(HttpMethod.Post, GraphQl, await CredentialAsync(ct), _appVersion, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) throw new NexusException($"Nexus Mods answered {(int)resp.StatusCode}");
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return ParseRequirements(doc.RootElement);
    }

    public static IReadOnlyList<NexusRequirement> ParseRequirements(JsonElement root)
    {
        var list = new List<NexusRequirement>();
        // Any level may be null (a hidden or removed mod, or an answer with "errors" and no data).
        static bool Prop(JsonElement e, string k, out JsonElement v)
        {
            v = default;
            return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out v);
        }
        if (!Prop(root, "data", out var d) || !Prop(d, "mod", out var m) || !Prop(m, "modRequirements", out var mr)
            || !Prop(mr, "nexusRequirements", out var nr) || !Prop(nr, "nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var n in nodes.EnumerateArray())
        {
            if (n.ValueKind != JsonValueKind.Object) continue;
            string S(string k) => n.TryGetProperty(k, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString() : "";
            var ext = n.TryGetProperty("externalRequirement", out var e) && e.ValueKind == JsonValueKind.True;
            var notes = S("notes");
            list.Add(new NexusRequirement(S("modId"), S("modName"), S("url"), ext, notes.Length == 0 ? null : notes));
        }
        return list;
    }

    /// <summary>Download URLs for a file. Free accounts need the key/expires from an nxm link.</summary>
    public async Task<IReadOnlyList<Uri>> DownloadLinksAsync(NxmLink link, CancellationToken ct = default)
    {
        var path = $"games/{link.Game}/mods/{link.ModId}/files/{link.FileId}/download_link.json";
        if (link.Key is not null && link.Expires is not null)
            path += $"?key={Uri.EscapeDataString(link.Key)}&expires={Uri.EscapeDataString(link.Expires)}";
        using var doc = await GetJsonAsync(path, ct);
        return ParseLinks(doc.RootElement);
    }

    public static IReadOnlyList<Uri> ParseLinks(JsonElement root)
    {
        var list = new List<Uri>();
        if (root.ValueKind != JsonValueKind.Array) return list;
        foreach (var e in root.EnumerateArray())
            if (e.TryGetProperty("URI", out var u) && u.ValueKind == JsonValueKind.String && Uri.TryCreate(u.GetString(), UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps)
                list.Add(uri);
        return list;
    }

    public async Task DownloadAsync(Uri uri, string destination, IProgress<(long Done, long? Total)>? progress = null, CancellationToken ct = default)
    {
        using var req = NexusHttp.DownloadRequest(uri);
        using var resp = await NexusHttp.Downloads.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode) throw new NexusException($"download failed ({(int)resp.StatusCode})");
        var total = resp.Content.Headers.ContentLength;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var tmp = destination + ".part";
        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(tmp))
        {
            var buf = new byte[1 << 16];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                progress?.Report((done, total));
            }
        }
        File.Move(tmp, destination, overwrite: true);
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct)
    {
        using var req = NexusHttp.ApiRequest(HttpMethod.Get, path, await CredentialAsync(ct), _appVersion);
        HttpResponseMessage resp;
        try { resp = await Http.SendAsync(req, ct); }
        catch (HttpRequestException ex) { throw new NexusException("Nexus Mods is not reachable: " + ex.Message); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new NexusException("Nexus Mods did not answer in time"); }
        using (resp)
        {
            NexusLimits.Observe(NexusLimits.Parse(resp.Headers, DateTimeOffset.UtcNow));
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new NexusException((int)resp.StatusCode switch
                {
                    401 => "Nexus Mods did not accept the login; log in again or paste your key again in Settings",
                    403 => "Nexus Mods refused the download. Free accounts download with the Mod Manager Download button on the website",
                    404 => "Nexus Mods does not know this file",
                    410 => "the download link expired; click Mod Manager Download again",
                    429 => "Nexus Mods rate limit reached; try again later",
                    var c => $"Nexus Mods answered {c}",
                }) { RateLimited = (int)resp.StatusCode == 429, NotFound = (int)resp.StatusCode == 404 };
            try { return JsonDocument.Parse(body); }
            catch (JsonException) { throw new NexusException("unexpected answer from Nexus Mods"); }
        }
    }

    public static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':' }).ToHashSet();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
        return clean.Length == 0 ? "download.zip" : clean;
    }

    public void Dispose() => _own?.Dispose(); // the shared client stays for the next action
}
