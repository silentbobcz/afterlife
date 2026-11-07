using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;


public class NexusAccountTests
{
    [Fact]
    public void Nexus_account_reads_premium_in_both_spellings()
    {
        using var a = System.Text.Json.JsonDocument.Parse("""{"name":"V","is_premium":true,"is_supporter":false}""");
        using var b = System.Text.Json.JsonDocument.Parse("""{"name":"Jackie","is_premium?":false,"is_supporter?":true}""");
        Assert.Equal("Premium", NexusClient.ParseAccount(a.RootElement).Tier);
        Assert.Equal("Supporter", NexusClient.ParseAccount(b.RootElement).Tier);
        using var links = System.Text.Json.JsonDocument.Parse("""[{"name":"CDN","URI":"https://cf-files.nexusmods.com/x.zip"},{"URI":"http://insecure/x"}]""");
        Assert.Single(NexusClient.ParseLinks(links.RootElement));
        Assert.Equal("a_b.zip", NexusClient.SafeFileName("a/b.zip"));
    }
}

public class NxmLinkTests
{
    [Theory]
    [InlineData("nxm://cyberpunk2077/mods/107/files/12345?key=abc&expires=1700000000&user_id=9", true, "107", "12345", "abc")]
    [InlineData("nxm://Cyberpunk2077/mods/4198/files/1", true, "4198", "1", null)]
    [InlineData("nxm://skyrimspecialedition/mods/1/files/2", false, "1", "2", null)]
    public void Nxm_links_are_parsed(string text, bool cyberpunk, string mod, string file, string? key)
    {
        Assert.True(NxmLink.TryParse(text, out var l));
        Assert.Equal(cyberpunk, l.IsCyberpunk);
        Assert.Equal(mod, l.ModId);
        Assert.Equal(file, l.FileId);
        Assert.Equal(key, l.Key);
    }

    [Theory]
    [InlineData("https://www.nexusmods.com/cyberpunk2077/mods/107")]
    [InlineData("nxm://cyberpunk2077/mods/abc/files/1")]
    [InlineData("nxm://cyberpunk2077/collections/xyz")]
    [InlineData("")]
    public void Bad_nxm_links_are_rejected(string text) => Assert.False(NxmLink.TryParse(text, out _));
}

public class NexusFileNameTests
{
    [Theory]
    [InlineData("Mod Settings v0.2.27 for 2.50-4885-0.2.27-2026-10-26T22-14Z-cB3xB00IB.zip", "Mod Settings v0.2.27 for 2.50", "0.2.27", "4885")]
    [InlineData("Mod Settings v0.2.27 for 2.50 4885 0.2.27 2026-10-26T22-14Z cB3xB00IB.zip", "Mod Settings v0.2.27 for 2.50", "0.2.27", "4885")]
    [InlineData("Native Settings UI-3518-1-97-1700000000.zip", "Native Settings UI", "1.97", "3518")]
    public void Recognizes_nexus_download_names(string file, string name, string version, string id)
    {
        var g = PackageLibrary.GuessFromFileName(file);
        Assert.Equal((name, version, id), (g.Name, g.Version, g.NexusModId));
    }
}

public class DownloadHistoryTests
{
    [Fact]
    public void Download_history_keeps_newest_first_without_duplicates()
    {
        using var t = new TestEnv();
        DownloadHistory.Record(t.C.Store, new NexusDownload("107", "1", "CET", "1.0", "cet.zip", DateTimeOffset.UtcNow.AddDays(-1)));
        DownloadHistory.Record(t.C.Store, new NexusDownload("4198", "2", "ArchiveXL", "1.2", "axl.zip", DateTimeOffset.UtcNow));
        DownloadHistory.Record(t.C.Store, new NexusDownload("107", "1", "CET", "1.0", "cet.zip", DateTimeOffset.UtcNow));
        var list = DownloadHistory.List(t.C.Store);
        Assert.Equal(["107", "4198"], list.Select(d => d.ModId).ToArray());
        Assert.Equal("https://www.nexusmods.com/cyberpunk2077/mods/107", list[0].Url);
    }
}

public class UpdateTests
{
    private static NexusFiles Files() => NexusClient.ParseFiles("107", System.Text.Json.JsonDocument.Parse("""
        {"files":[
          {"file_id":1,"name":"CET","version":"1.30","category_name":"OLD_VERSION","uploaded_timestamp":100,"file_name":"cet130.zip"},
          {"file_id":2,"name":"CET","version":"1.35","category_name":"MAIN","uploaded_timestamp":200,"file_name":"cet135.zip"},
          {"file_id":3,"name":"CET hotfix","version":"1.35.1","category_name":"MAIN","uploaded_timestamp":300,"file_name":"cet1351.zip"}],
         "file_updates":[{"old_file_id":1,"new_file_id":2},{"old_file_id":2,"new_file_id":3}]}
        """).RootElement);

    [Fact]
    public void Follows_replaced_files_to_the_newest_M4()
    {
        var u = NexusUpdates.Find(Files(), "1", "1.30", DateTimeOffset.UtcNow);
        Assert.NotNull(u);
        Assert.Equal("3", u!.FileId);
        Assert.True(u.Exact);
        Assert.Null(NexusUpdates.Find(Files(), "3", "1.35.1", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Without_a_file_id_compares_the_main_version_M4()
    {
        var u = NexusUpdates.Find(Files(), null, "1.30", DateTimeOffset.UtcNow);
        Assert.Equal("1.35.1", u!.Version);
        Assert.False(u.Exact);
        Assert.Null(NexusUpdates.Find(Files(), null, "v1.35.1", DateTimeOffset.UtcNow));
        Assert.NotNull(NexusUpdates.Find(Files(), null, "1.35.10", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Checks_at_most_once_a_day_M4()
    {
        using var t = new TestEnv();
        var now = DateTimeOffset.UtcNow;
        Assert.False(NexusUpdates.IsFresh(t.C.Store, "pkg-x", now));
        NexusUpdates.Save(t.C.Store, "pkg-x", null, now);
        Assert.True(NexusUpdates.IsFresh(t.C.Store, "pkg-x", now.AddHours(5)));
        Assert.False(NexusUpdates.IsFresh(t.C.Store, "pkg-x", now.AddHours(25)));
    }
}

public class UpdateFeedTests
{
    [Fact]
    public void Hourly_check_reads_files_only_for_changed_mods()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var hourAgo = now.AddHours(-1);
        var json = "[{\"mod_id\":1,\"latest_file_update\":" + now.AddMinutes(-20).ToUnixTimeSeconds() + ",\"latest_mod_activity\":0},"
                 + "{\"mod_id\":2,\"latest_file_update\":" + now.AddHours(-5).ToUnixTimeSeconds() + ",\"latest_mod_activity\":0}]";
        var feed = NexusClient.ParseUpdated(System.Text.Json.JsonDocument.Parse(json).RootElement);
        Assert.Equal("1d", NexusUpdates.FeedPeriod(hourAgo, now));
        Assert.Equal("1w", NexusUpdates.FeedPeriod(now.AddDays(-2), now));
        Assert.Null(NexusUpdates.FeedPeriod(now.AddDays(40), now.AddDays(80)));
        var due = NexusUpdates.DueAfterFeed([("a", "1", hourAgo), ("b", "2", hourAgo), ("c", "3", hourAgo), ("d", "4", null)], feed, now);
        Assert.Equal(["a", "d"], due);
    }
}

public class SsoTests
{
    [Fact]
    public void Reads_sso_messages_and_stays_hidden_without_a_slug()
    {
        Assert.False(NexusSso.Available);
        Assert.Null(NexusSso.ParseMessage("""{"success":true,"data":{"connection_token":"t"},"error":null}""", out var c));
        Assert.True(c);
        Assert.Equal("KEY", NexusSso.ParseMessage("""{"success":true,"data":{"api_key":"KEY"},"error":null}""", out _));
        Assert.Throws<NexusException>(() => NexusSso.ParseMessage("""{"success":false,"data":null,"error":"bad"}""", out _));
    }
}
