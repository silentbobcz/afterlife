using Afterlife.Core;
using System.Net.Http.Headers;
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

public sealed class NexusException(string message) : Exception(message);

/// <summary>A mod the Nexus page lists as required (P40/P43).</summary>
public sealed record NexusRequirement(string ModId, string Name, string Url, bool External, string? Notes);

/// <summary>P43: what the Nexus page says about a mod, cached in the library database.</summary>
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

/// <summary>P48: one download from Nexus Mods through AFTERLIFE (kept even after the mod is deleted).</summary>
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

/// <summary>M4: a newer file for a package. Exact = the author marked our file as replaced; otherwise the version of the main file differs.</summary>
public sealed record NexusUpdate(string ModId, string FileId, string? Version, string Name, bool Exact, DateTimeOffset CheckedAt)
{
    public string Url => $"https://www.nexusmods.com/{NxmLink.Cyberpunk}/mods/{ModId}?tab=files&file_id={FileId}";
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
    public static DateTimeOffset? CheckedAt(AfterlifeStore store, string packageId)
    {
        var v = store.GetMeta(Key(packageId));
        if (v is null) return null;
        if (v.StartsWith(None, StringComparison.Ordinal))
            return long.TryParse(v[(None.Length + 1)..], out var t) ? DateTimeOffset.FromUnixTimeSeconds(t) : null;
        return Load(store, packageId)?.CheckedAt;
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

    public static NexusUpdate? Load(AfterlifeStore store, string packageId)
    {
        var v = store.GetMeta(Key(packageId));
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
    /// <summary>P49: Nexus has no optional flag on requirements; the author's note says it.</summary>
    public static bool IsOptionalNote(string? notes) => notes is { } n &&
        (n.TrimStart().StartsWith("optional", StringComparison.OrdinalIgnoreCase) || n.TrimStart().StartsWith("recommended", StringComparison.OrdinalIgnoreCase));

    private static string Key(string modId) => "nexus:mod:" + modId;

    public static NexusModInfo? Load(AfterlifeStore store, string? modId)
    {
        if (modId is null) return null;
        var json = store.GetMeta(Key(modId));
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
/// Every user enters their own personal API key (D8); it never leaves this machine except to api.nexusmods.com.
/// </summary>
public sealed class NexusClient : IDisposable
{
    public const string ApiBase = "https://api.nexusmods.com/v1/";
    public const string KeyPage = "https://www.nexusmods.com/settings/api-keys";
    public const string CyberpunkPage = "https://www.nexusmods.com/games/cyberpunk2077/mods";
    public const string GraphQl = "https://api.nexusmods.com/v2/graphql";
    public const string CyberpunkGameId = "3333";

    private readonly HttpClient _http;

    public NexusClient(string apiKey, string appVersion, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri(ApiBase);
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Add("apikey", apiKey);
        _http.DefaultRequestHeaders.Add("Application-Name", "AFTERLIFE");
        _http.DefaultRequestHeaders.Add("Application-Version", appVersion);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AFTERLIFE", appVersion));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
    /// P43: summary, author, picture and counts (API v1) plus the requirements listed on the mod page (GraphQL v2).
    /// Requirements are best effort: when GraphQL fails the rest is still returned.
    /// </summary>
    /// <summary>P51: one revision of a collection (latest when no number).</summary>
    public async Task<CollectionRevisionInfo> CollectionRevisionAsync(CollectionLink link, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { query = CollectionLogic.RevisionQuery, variables = new { slug = link.Slug, revision = link.Revision, domain = link.Game } });
        using var req = new HttpRequestMessage(HttpMethod.Post, GraphQl) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        HttpResponseMessage resp;
        try { resp = await _http.SendAsync(req, ct); }
        catch (HttpRequestException ex) { throw new NexusException("Nexus Mods is not reachable: " + ex.Message); }
        using (resp)
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new NexusException($"Nexus Mods answered {(int)resp.StatusCode}");
            using var doc = JsonDocument.Parse(text);
            return CollectionLogic.FromGraphQl(link.Slug, doc.RootElement);
        }
    }

    /// <summary>P51: downloads the collection bundle (.7z with collection.json and bundled files) into the given file.</summary>
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

    /// <summary>M4: files of a mod and which file replaced which.</summary>
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
        using var req = new HttpRequestMessage(HttpMethod.Post, GraphQl) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) throw new NexusException($"Nexus Mods answered {(int)resp.StatusCode}");
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return ParseRequirements(doc.RootElement);
    }

    public static IReadOnlyList<NexusRequirement> ParseRequirements(JsonElement root)
    {
        var list = new List<NexusRequirement>();
        if (!root.TryGetProperty("data", out var d) || !d.TryGetProperty("mod", out var m) || m.ValueKind != JsonValueKind.Object
            || !m.TryGetProperty("modRequirements", out var mr) || mr.ValueKind != JsonValueKind.Object
            || !mr.TryGetProperty("nexusRequirements", out var nr) || !nr.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var n in nodes.EnumerateArray())
        {
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
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        using var plain = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        plain.DefaultRequestHeaders.UserAgent.ParseAdd("AFTERLIFE");
        using var resp = await plain.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
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
        HttpResponseMessage resp;
        try { resp = await _http.GetAsync(path, ct); }
        catch (HttpRequestException ex) { throw new NexusException("Nexus Mods is not reachable: " + ex.Message); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new NexusException("Nexus Mods did not answer in time"); }
        using (resp)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new NexusException((int)resp.StatusCode switch
                {
                    401 => "the API key was not accepted; paste it again from your Nexus Mods account",
                    403 => "Nexus Mods refused the download. Free accounts download with the Mod Manager Download button on the website",
                    404 => "Nexus Mods does not know this file",
                    410 => "the download link expired; click Mod Manager Download again",
                    429 => "Nexus Mods rate limit reached; try again later",
                    var c => $"Nexus Mods answered {c}",
                });
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

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Nexus Mods single sign-on (wss://sso.nexusmods.com): the user approves AFTERLIFE on the website and the key comes
/// back without copying. Needs the application slug Nexus Mods issues after registration (API Acceptable Use Policy);
/// until then <see cref="ApplicationSlug"/> is null and the app uses the personal key.
/// </summary>
public static class NexusSso
{
    public const string Endpoint = "wss://sso.nexusmods.com";
    /// <summary>Issued by Nexus Mods after the application is registered (support@nexusmods.com).</summary>
    public static string? ApplicationSlug { get; set; }
    public static bool Available => !string.IsNullOrWhiteSpace(ApplicationSlug);

    public static string ApprovalPage(string id) => $"https://www.nexusmods.com/sso?id={Uri.EscapeDataString(id)}&application={Uri.EscapeDataString(ApplicationSlug ?? "")}";

    public static async Task<string> LoginAsync(Action<string> openBrowser, CancellationToken ct)
    {
        if (!Available) throw new NexusException("Log in with Nexus Mods is not available in this version; paste your personal key");
        using var ws = new System.Net.WebSockets.ClientWebSocket();
        await ws.ConnectAsync(new Uri(Endpoint), ct);
        var id = Guid.NewGuid().ToString();
        await SendAsync(ws, JsonSerializer.Serialize(new { id, token = (string?)null, protocol = 2 }), ct);
        var opened = false;
        while (!ct.IsCancellationRequested && ws.State == System.Net.WebSockets.WebSocketState.Open)
        {
            var text = await ReceiveAsync(ws, ct);
            if (text is null) break;
            var key = ParseMessage(text, out var connected);
            if (connected && !opened) { opened = true; openBrowser(ApprovalPage(id)); }
            if (key is not null)
            {
                try { await ws.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch (System.Net.WebSockets.WebSocketException) { }
                return key;
            }
        }
        throw new NexusException("the login was not finished");
    }

    /// <summary>Returns the API key when the message carries it; connected = the server accepted the request.</summary>
    public static string? ParseMessage(string text, out bool connected)
    {
        connected = false;
        using var doc = JsonDocument.Parse(text);
        var r = doc.RootElement;
        if (!r.TryGetProperty("success", out var ok) || ok.ValueKind != JsonValueKind.True)
        {
            var err = r.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : "rejected";
            throw new NexusException("Nexus Mods login: " + err);
        }
        if (!r.TryGetProperty("data", out var d) || d.ValueKind != JsonValueKind.Object) return null;
        if (d.TryGetProperty("connection_token", out _)) connected = true;
        return d.TryGetProperty("api_key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
    }

    private static Task SendAsync(System.Net.WebSockets.ClientWebSocket ws, string text, CancellationToken ct) =>
        ws.SendAsync(System.Text.Encoding.UTF8.GetBytes(text), System.Net.WebSockets.WebSocketMessageType.Text, true, ct);

    private static async Task<string?> ReceiveAsync(System.Net.WebSockets.ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        while (true)
        {
            var r = await ws.ReceiveAsync(buffer, ct);
            if (r.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) return null;
            ms.Write(buffer, 0, r.Count);
            if (r.EndOfMessage) return System.Text.Encoding.UTF8.GetString(ms.ToArray());
            if (ms.Length > 1 << 20) throw new NexusException("unexpected answer from Nexus Mods");
        }
    }
}

/// <summary>
/// P86: the picture of a mod from its Nexus page, kept in the library so it is fetched once per mod (only for mods
/// in the library, never in bulk). Missing or failing pictures just leave the details without one.
/// </summary>
public static class ModImageCache
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    public const long MaxBytes = 4 * 1024 * 1024;

    public static string PathFor(LibraryPaths lib, string modId, string url)
    {
        var ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp")) ext = ".jpg";
        return Path.Combine(lib.Images, $"{modId}{ext}");
    }

    public static string? Cached(LibraryPaths lib, string modId, string? url) =>
        url is null || !Uri.IsWellFormedUriString(url, UriKind.Absolute) ? null : File.Exists(PathFor(lib, modId, url)) ? PathFor(lib, modId, url) : null;

    public static async Task<string?> EnsureAsync(LibraryPaths lib, string modId, string? url, CancellationToken ct = default)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
        var path = PathFor(lib, modId, url);
        if (File.Exists(path)) return path;
        Directory.CreateDirectory(lib.Images);
        using var resp = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode || resp.Content.Headers.ContentLength > MaxBytes) return null;
        if (resp.Content.Headers.ContentType?.MediaType is { } mt && !mt.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length == 0 || bytes.Length > MaxBytes) return null;
        var tmp = path + ".part";
        await File.WriteAllBytesAsync(tmp, bytes, ct);
        File.Move(tmp, path, true);
        return path;
    }
}
