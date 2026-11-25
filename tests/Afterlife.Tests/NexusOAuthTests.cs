using System.Net;
using System.Text.Json;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

/// <summary>The tests change the static client ID, port and refresh hook, so they run one after another in this class.</summary>
public class NexusOAuthTests
{
    /// <summary>Answers the token endpoint and the API; records what each request carried.</summary>
    private sealed class FakeNexus : HttpMessageHandler
    {
        public readonly List<(string Url, string? Auth, string? ApiKey, string? Form)> Seen = [];
        public int TokenStatus = 200;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var form = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Seen.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("apikey", out var k) ? k.First() : null, form));
            if (request.RequestUri.ToString() == NexusOAuth.TokenUrl)
                return new HttpResponseMessage((HttpStatusCode)TokenStatus)
                {
                    Content = new StringContent("""{"access_token":"NEW-ACCESS","refresh_token":"NEW-REFRESH","expires_in":21600,"token_type":"Bearer"}"""),
                };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"name":"V","is_premium":true}""") };
        }
    }

    [Fact]
    public void Pkce_challenge_matches_the_rfc_example()
    {
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", NexusOAuth.Challenge("dBjftJeZ4CVP-mJ92IqVc8_xF6r4dl0VHhxDB_6xOgQ"));
        var (verifier, challenge) = NexusOAuth.NewPkce();
        Assert.Equal(43, verifier.Length);
        Assert.Equal(NexusOAuth.Challenge(verifier), challenge);
    }

    [Fact]
    public void Tokens_round_trip_and_a_personal_key_stays_a_key()
    {
        var t = new NexusTokens("a", "r", 1_800_000_000, "login");
        Assert.Equal(t, NexusTokens.From(t.Serialize()));
        Assert.Null(NexusTokens.From("personal-api-key"));
        Assert.Null(NexusTokens.From("""{"type":"other","access":"a","refresh":"r"}"""));
        Assert.True(t.Expired(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000 - 30)));
        Assert.False(t.Expired(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000 - 600)));
    }

    [Fact]
    public void Callback_needs_our_state_and_a_code()
    {
        Assert.Equal(("abc", null), NexusOAuth.ParseCallback("/callback?code=abc&state=s1", "s1"));
        Assert.Null(NexusOAuth.ParseCallback("/callback?code=abc&state=other", "s1").Code);
        Assert.Contains("refused", NexusOAuth.ParseCallback("/callback?error=access_denied&state=s1", "s1").Error);
        Assert.Null(NexusOAuth.ParseCallback("/callback?state=s1", "s1").Code);
    }

    [Fact]
    public void Token_answer_counts_the_expiry_from_now()
    {
        using var doc = JsonDocument.Parse("""{"access_token":"A","refresh_token":"R","expires_in":100}""");
        var t = NexusOAuth.ParseTokens(doc.RootElement, "l", DateTimeOffset.FromUnixTimeSeconds(1000));
        Assert.Equal(new NexusTokens("A", "R", 1100, "l"), t);
        using var bad = JsonDocument.Parse("""{"access_token":"A"}""");
        Assert.Throws<NexusException>(() => NexusOAuth.ParseTokens(bad.RootElement, "l", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Requests_carry_the_bearer_token_and_refresh_it_before_it_expires()
    {
        var fake = new FakeNexus();
        string? saved = null;
        NexusOAuth.Refreshed = c => saved = c;
        try
        {
            var old = new NexusTokens("OLD-ACCESS", "OLD-REFRESH", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10, Guid.NewGuid().ToString("N"));
            using var client = new NexusClient(old.Serialize(), "1.0", fake);
            Assert.True((await client.ValidateAsync()).IsPremium);

            Assert.Equal(NexusOAuth.TokenUrl, fake.Seen[0].Url);
            Assert.Contains("grant_type=refresh_token", fake.Seen[0].Form);
            Assert.Contains("refresh_token=OLD-REFRESH", fake.Seen[0].Form);
            Assert.Equal("Bearer NEW-ACCESS", fake.Seen[1].Auth);
            Assert.Null(fake.Seen[1].ApiKey);
            Assert.Equal("NEW-ACCESS", NexusTokens.From(saved!)!.Access);

            await client.ValidateAsync(); // still fresh: no second refresh
            Assert.Single(fake.Seen, s => s.Url == NexusOAuth.TokenUrl);
        }
        finally { NexusOAuth.Refreshed = null; }
    }

    [Fact]
    public async Task A_refused_refresh_signs_the_user_out()
    {
        var fake = new FakeNexus { TokenStatus = 400 };
        var old = new NexusTokens("A", "R", 0, Guid.NewGuid().ToString("N"));
        using var client = new NexusClient(old.Serialize(), "1.0", fake);
        var ex = await Assert.ThrowsAsync<NexusException>(() => client.ValidateAsync());
        Assert.True(ex.SignedOut);
    }

    [Fact]
    public async Task A_personal_key_still_goes_in_the_apikey_header()
    {
        var fake = new FakeNexus();
        using var client = new NexusClient("personal-key", "1.0", fake);
        await client.ValidateAsync();
        Assert.Equal("personal-key", fake.Seen.Single().ApiKey);
        Assert.Null(fake.Seen.Single().Auth);
    }

    [Fact]
    public async Task Login_receives_the_code_on_the_loopback_listener()
    {
        var (clientId, port) = (NexusOAuth.ClientId, NexusOAuth.Port);
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        NexusOAuth.Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        NexusOAuth.ClientId = "test-client";
        var fake = new FakeNexus();
        Task? browser = null;
        try
        {
            void Open(string url)
            {
                var query = new Uri(url).Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
                Assert.Equal("test-client", query["client_id"]);
                Assert.Equal("S256", query["code_challenge_method"]);
                browser = Task.Run(async () =>
                {
                    using var http = new HttpClient();
                    await http.GetStringAsync($"{query["redirect_uri"]}?code=THE-CODE&state={query["state"]}");
                });
            }
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var credential = await NexusOAuth.LoginAsync(Open, cts.Token, fake);
            await browser!;
            Assert.Equal("NEW-ACCESS", NexusTokens.From(credential)!.Access);
            var form = fake.Seen.Single().Form!;
            Assert.Contains("grant_type=authorization_code", form);
            Assert.Contains("code=THE-CODE", form);
            Assert.Contains("code_verifier=", form);
        }
        finally { (NexusOAuth.ClientId, NexusOAuth.Port) = (clientId, port); }
    }
}
