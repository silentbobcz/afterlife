using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Afterlife.Infrastructure;

/// <summary>
/// What a <see cref="NexusClient"/> signs its requests with: a personal API key (test builds) or OAuth tokens from
/// <see cref="NexusOAuth"/>. Both travel through the app as one string, so the key store, the settings and every
/// caller stay the same; OAuth tokens are a small JSON object that a personal key can never look like.
/// </summary>
public sealed record NexusTokens(string Access, string Refresh, long ExpiresAt, string Login)
{
    private const string Marker = "nexus-oauth";

    /// <summary>The tokens inside a stored credential, or null when it is a personal API key.</summary>
    public static NexusTokens? From(string credential)
    {
        if (!credential.StartsWith('{')) return null;
        try
        {
            using var doc = JsonDocument.Parse(credential);
            var r = doc.RootElement;
            string? S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            if (S("type") != Marker || S("access") is not { Length: > 0 } a || S("refresh") is not { Length: > 0 } f) return null;
            var exp = r.TryGetProperty("expires", out var e) && e.TryGetInt64(out var x) ? x : 0;
            return new NexusTokens(a, f, exp, S("login") ?? "");
        }
        catch (JsonException) { return null; }
    }

    public string Serialize() =>
        JsonSerializer.Serialize(new { type = Marker, access = Access, refresh = Refresh, expires = ExpiresAt, login = Login });

    /// <summary>True when the access token is gone or ends within a minute.</summary>
    public bool Expired(DateTimeOffset now) => ExpiresAt - 60 <= now.ToUnixTimeSeconds();
}

/// <summary>
/// Log in with Nexus Mods through OAuth 2.0 with PKCE (a public application, no client secret): the browser asks the
/// user to approve AFTERLIFE, Nexus Mods sends a code back to a one-shot listener on 127.0.0.1, and the code becomes an
/// access token and a refresh token. The access token goes on API v1 and v2 requests as "Authorization: Bearer".
/// Available once Nexus Mods has issued the client ID; until then the app uses the personal key.
/// </summary>
public static class NexusOAuth
{
    public const string AuthorizeUrl = "https://users.nexusmods.com/oauth/authorize";
    public const string TokenUrl = "https://users.nexusmods.com/oauth/token";
    public const int DefaultPort = 47213;

    /// <summary>Issued by Nexus Mods for AFTERLIFE; null until the registration is approved.</summary>
    private const string? IssuedClientId = null;

    /// <summary>The client ID in use. The environment variables let a tester try the flow with another client.</summary>
    public static string? ClientId { get; set; } =
        Environment.GetEnvironmentVariable("AFTERLIFE_NEXUS_CLIENT_ID") is { Length: > 0 } id ? id : IssuedClientId;

    public static int Port { get; set; } =
        int.TryParse(Environment.GetEnvironmentVariable("AFTERLIFE_NEXUS_REDIRECT_PORT"), out var p) && p is > 0 and < 65536 ? p : DefaultPort;

    public static string RedirectUri => $"http://127.0.0.1:{Port}/callback";
    public static bool Available => !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>Called with the new credential after every refresh so the app can store it.</summary>
    public static Action<string>? Refreshed { get; set; }

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static NexusTokens? _latest;

    /// <summary>A random PKCE verifier (43 characters) and its S256 challenge.</summary>
    public static (string Verifier, string Challenge) NewPkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (verifier, Challenge(verifier));
    }

    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static string AuthorizePage(string challenge, string state) =>
        AuthorizeUrl + "?" + Form(("client_id", ClientId ?? ""), ("response_type", "code"), ("scope", ""), ("redirect_uri", RedirectUri),
            ("state", state), ("code_challenge_method", "S256"), ("code_challenge", challenge));

    /// <summary>Opens the approval page, waits for the code on 127.0.0.1 and returns the credential to store.</summary>
    public static async Task<string> LoginAsync(Action<string> openBrowser, CancellationToken ct, HttpMessageHandler? handler = null)
    {
        if (!Available) throw new NexusException("Log in with Nexus Mods is not available in this version; paste your personal key");
        var (verifier, challenge) = NewPkce();
        var state = Guid.NewGuid().ToString("N");
        var listener = new TcpListener(IPAddress.Loopback, Port);
        try { listener.Start(); }
        catch (SocketException) { throw new NexusException($"port {Port} on this PC is in use by another program; close it and try again"); }
        string code;
        try
        {
            openBrowser(AuthorizePage(challenge, state));
            code = await WaitForCodeAsync(listener, state, ct);
        }
        finally { listener.Stop(); }
        var tokens = await TokenRequestAsync([("grant_type", "authorization_code"), ("client_id", ClientId!), ("redirect_uri", RedirectUri),
            ("code", code), ("code_verifier", verifier)], Guid.NewGuid().ToString("N"), handler, ct);
        _latest = tokens;
        return tokens.Serialize();
    }

    /// <summary>
    /// The tokens to sign the next request with: the newest known for this login, refreshed when about to expire.
    /// A refused refresh means the user revoked AFTERLIFE on the website and has to log in again.
    /// </summary>
    public static async Task<NexusTokens> FreshAsync(NexusTokens tokens, CancellationToken ct, HttpMessageHandler? handler = null)
    {
        await Gate.WaitAsync(ct);
        try
        {
            if (_latest is { } l && l.Login == tokens.Login && l.ExpiresAt > tokens.ExpiresAt) tokens = l;
            if (!tokens.Expired(DateTimeOffset.UtcNow)) return tokens;
            var fresh = await TokenRequestAsync([("grant_type", "refresh_token"), ("client_id", ClientId ?? ""), ("refresh_token", tokens.Refresh)],
                tokens.Login, handler, ct);
            _latest = fresh;
            Refreshed?.Invoke(fresh.Serialize());
            return fresh;
        }
        finally { Gate.Release(); }
    }

    /// <summary>Reads "GET /callback?code=…&amp;state=…" from the browser, answers with a short page and returns the code.</summary>
    private static async Task<string> WaitForCodeAsync(TcpListener listener, string state, CancellationToken ct)
    {
        while (true)
        {
            using var socket = await listener.AcceptTcpClientAsync(ct);
            await using var stream = socket.GetStream();
            var line = await ReadRequestLineAsync(stream, ct);
            var target = line?.Split(' ') is [_, var t, ..] ? t : "";
            if (!target.StartsWith("/callback", StringComparison.Ordinal)) { await RespondAsync(stream, 404, "Not found", ct); continue; }
            var (code, error) = ParseCallback(target, state);
            await RespondAsync(stream, 200, code is not null
                ? "AFTERLIFE is logged in to Nexus Mods. You can close this tab."
                : "AFTERLIFE was not logged in. You can close this tab and try again.", ct);
            return code ?? throw new NexusException(error ?? "the login was not finished");
        }
    }

    /// <summary>The code from the callback, or the reason it is missing (refused, or a state that is not ours).</summary>
    public static (string? Code, string? Error) ParseCallback(string target, string state)
    {
        var q = target.IndexOf('?') is var i and >= 0 ? target[(i + 1)..] : "";
        var map = q.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .GroupBy(p => Uri.UnescapeDataString(p[0]))
            .ToDictionary(g => g.Key, g => g.First() is [_, var v] ? Uri.UnescapeDataString(v.Replace('+', ' ')) : "");
        if (map.TryGetValue("error", out var err)) return (null, err == "access_denied" ? "the login was refused on Nexus Mods" : "Nexus Mods login: " + err);
        if (!map.TryGetValue("state", out var s) || s != state) return (null, "the login answer did not match; try again");
        return map.TryGetValue("code", out var c) && c.Length > 0 ? (c, null) : (null, "Nexus Mods sent no login code");
    }

    /// <summary>Turns a token endpoint answer into tokens; the expiry is counted from now.</summary>
    public static NexusTokens ParseTokens(JsonElement r, string login, DateTimeOffset now)
    {
        string? S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        if (S("access_token") is not { Length: > 0 } access || S("refresh_token") is not { Length: > 0 } refresh)
            throw new NexusException("unexpected answer from Nexus Mods");
        var life = r.TryGetProperty("expires_in", out var e) && e.TryGetInt64(out var x) ? x : 3600;
        return new NexusTokens(access, refresh, now.ToUnixTimeSeconds() + life, login);
    }

    private static async Task<NexusTokens> TokenRequestAsync((string, string)[] fields, string login, HttpMessageHandler? handler, CancellationToken ct)
    {
        using var own = handler is null ? null : NexusHttp.WithHandler(handler);
        var http = own ?? NexusHttp.Api;
        using var req = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = new FormUrlEncodedContent(fields.Select(f => KeyValuePair.Create(f.Item1, f.Item2))) };
        req.Headers.UserAgent.ParseAdd("AFTERLIFE");
        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req, ct); }
        catch (HttpRequestException ex) { throw new NexusException("Nexus Mods is not reachable: " + ex.Message); }
        using (resp)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            var code = (int)resp.StatusCode;
            if (code is >= 400 and < 500) throw new NexusException("the Nexus Mods login expired or was revoked; log in again in Settings") { SignedOut = true };
            if (!resp.IsSuccessStatusCode) throw new NexusException($"Nexus Mods answered {code}");
            try
            {
                using var doc = JsonDocument.Parse(body);
                return ParseTokens(doc.RootElement, login, DateTimeOffset.UtcNow);
            }
            catch (JsonException) { throw new NexusException("unexpected answer from Nexus Mods"); }
        }
    }

    private static async Task<string?> ReadRequestLineAsync(NetworkStream stream, CancellationToken ct)
    {
        var buf = new byte[8192];
        var total = 0;
        while (total < buf.Length)
        {
            var n = await stream.ReadAsync(buf.AsMemory(total), ct);
            if (n == 0) break;
            total += n;
            var text = Encoding.ASCII.GetString(buf, 0, total);
            if (text.IndexOf("\r\n", StringComparison.Ordinal) is var end and >= 0) return text[..end];
        }
        return null;
    }

    private static async Task RespondAsync(NetworkStream stream, int status, string message, CancellationToken ct)
    {
        var html = $"<!doctype html><meta charset=\"utf-8\"><title>AFTERLIFE</title><body style=\"background:#0b0b10;color:#e8e8e8;font-family:sans-serif;padding:40px\"><p>{WebUtility.HtmlEncode(message)}</p></body>";
        var body = Encoding.UTF8.GetBytes(html);
        var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : "Not Found")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(head, ct);
        await stream.WriteAsync(body, ct);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Form(params (string Key, string Value)[] fields) =>
        string.Join("&", fields.Select(f => Uri.EscapeDataString(f.Key) + "=" + Uri.EscapeDataString(f.Value)));
}
