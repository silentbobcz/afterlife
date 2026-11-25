using System.Net.Http.Headers;

namespace Afterlife.Infrastructure;

/// <summary>
/// The HTTP clients shared by every <see cref="NexusClient"/> and the mod picture cache, created on first use:
/// one for API calls (30 seconds), one for file downloads (30 minutes) and one for mod pictures (20 seconds).
/// The shared clients carry no default headers: the API key and the application headers go on each API request,
/// so a changed key takes effect at once and downloads from CDN links never receive the key.
/// An edition that must never connect calls <see cref="Disable"/> at start; no client is created after that.
/// </summary>
public static class NexusHttp
{
    public static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ImageTimeout = TimeSpan.FromSeconds(20);

    private static readonly Uri ApiBase = new(NexusClient.ApiBase);
    private static readonly Lazy<HttpClient> SharedApi = new(() => Create(new HttpClient(), ApiTimeout));
    private static readonly Lazy<HttpClient> SharedDownloads = new(() => Create(new HttpClient(), DownloadTimeout));
    private static readonly Lazy<HttpClient> SharedImages = new(() => Create(new HttpClient(), ImageTimeout));
    private static volatile bool _disabled;

    /// <summary>Stops every shared client from being created in this process (AFTERLIFE Offline).</summary>
    public static void Disable() => _disabled = true;

    public static bool IsDisabled => _disabled;

    public static HttpClient Api => Get(SharedApi);
    public static HttpClient Downloads => Get(SharedDownloads);
    public static HttpClient Images => Get(SharedImages);

    /// <summary>A client over the given handler with the API timeout, owned by the caller (tests inject answers this way).</summary>
    public static HttpClient WithHandler(HttpMessageHandler handler) => Create(new HttpClient(handler), ApiTimeout);

    /// <summary>
    /// A request to the Nexus Mods API with the credential (OAuth bearer token or personal key) and the application
/// headers on the request itself.
    /// A relative <paramref name="uri"/> is resolved against <see cref="NexusClient.ApiBase"/>.
    /// </summary>
    public static HttpRequestMessage ApiRequest(HttpMethod method, string uri, string credential, string appVersion, HttpContent? content = null)
    {
        var req = new HttpRequestMessage(method, new Uri(ApiBase, uri)) { Content = content };
        if (NexusTokens.From(credential) is { } tokens) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);
        else req.Headers.Add("apikey", credential);
        req.Headers.Add("Application-Name", "AFTERLIFE");
        req.Headers.Add("Application-Version", appVersion);
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("AFTERLIFE", appVersion));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
    }

    /// <summary>A plain download request (CDN links): only the user agent, never the API key.</summary>
    public static HttpRequestMessage DownloadRequest(Uri uri)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, uri);
        req.Headers.UserAgent.ParseAdd("AFTERLIFE");
        return req;
    }

    private static HttpClient Get(Lazy<HttpClient> client) =>
        _disabled ? throw new InvalidOperationException("AFTERLIFE Offline does not connect to Nexus Mods") : client.Value;

    private static HttpClient Create(HttpClient client, TimeSpan timeout)
    {
        client.Timeout = timeout;
        return client;
    }
}
