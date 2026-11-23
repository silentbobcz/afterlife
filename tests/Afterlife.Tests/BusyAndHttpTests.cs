using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

public class BusyCounterTests
{
    [Fact]
    public void Overlapping_actions_stay_busy_until_the_last_one_ends()
    {
        var busy = new BusyCounter();
        var flips = new List<bool>();
        busy.Changed += () => flips.Add(busy.IsBusy);

        var first = busy.Enter();
        var second = busy.Enter();
        first.Dispose(); // the first action ends while the second still runs
        Assert.True(busy.IsBusy);
        first.Dispose(); // a scope counts once
        Assert.Equal(1, busy.Count);
        second.Dispose();

        Assert.False(busy.IsBusy);
        Assert.Equal(new[] { true, false }, flips.ToArray()); // only the 0→1 and 1→0 transitions are announced
    }

    [Fact]
    public async Task A_failing_action_still_ends_its_scope()
    {
        var busy = new BusyCounter();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var scope = busy.Enter();
            await Task.Yield();
            throw new InvalidOperationException("failed");
        });
        Assert.False(busy.IsBusy);
    }
}

public class NexusHttpTests
{
    private sealed class Recorder : HttpMessageHandler
    {
        public List<HttpRequestMessage> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Seen.Add(request);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"name":"V","is_premium":true}""") });
        }
    }

    [Fact]
    public async Task The_key_goes_on_each_request_and_never_on_a_download()
    {
        var recorder = new Recorder();
        using (var client = new NexusClient("first-key", "1.0", recorder))
            Assert.True((await client.ValidateAsync()).IsPremium);
        var asked = Assert.Single(recorder.Seen);
        Assert.Equal("https://api.nexusmods.com/v1/users/validate.json", asked.RequestUri!.ToString());
        Assert.Equal("first-key", Assert.Single(asked.Headers.GetValues("apikey")));
        Assert.Equal("AFTERLIFE", Assert.Single(asked.Headers.GetValues("Application-Name")));

        using var download = NexusHttp.DownloadRequest(new Uri("https://cf-files.nexusmods.com/x.zip"));
        Assert.False(download.Headers.Contains("apikey"));
        Assert.Equal("AFTERLIFE", download.Headers.UserAgent.ToString());
    }
}
