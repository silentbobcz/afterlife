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

public class NexusLinkerTests
{
    /// <summary>Answers by the path of the request; an unknown path is a 404, like on Nexus Mods.</summary>
    private sealed class FakeNexus(Dictionary<string, (int Status, string Body)> answers) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Asked.Add(path);
            var (status, body) = answers.FirstOrDefault(a => path.EndsWith(a.Key, StringComparison.Ordinal)).Value is { Body: not null } hit ? hit : (404, "{}");
            return Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)status) { Content = new StringContent(body) });
        }
    }

    private const string Md5 = "0123456789abcdef0123456789abcdef";

    private static PackageManifest Manual(string archive, string? guess, string? md5 = Md5, string id = "pkg-1") =>
        new(id, "mod-" + id, "Local name", "1.0", PackageOrigin.Manual, null, "digest", [], [], [], null, guess, null, archive, md5, DateTimeOffset.UtcNow);

    private static string Hit(string modId, string fileId, string fileName, string game = "cyberpunk2077") =>
        "{\"mod\":{\"name\":\"Mod " + modId + "\",\"mod_id\":" + modId + ",\"domain_name\":\"" + game + "\"},"
        + "\"file_details\":{\"file_id\":" + fileId + ",\"file_name\":\"" + fileName + "\",\"version\":\"1.0\"}}";

    private static string Files(params (string Id, string FileName, long Uploaded)[] files) =>
        "{\"files\":[" + string.Join(",", files.Select(f => "{\"file_id\":" + f.Id + ",\"name\":\"Main\",\"version\":\"1.0\",\"category_name\":\"MAIN\","
            + "\"uploaded_timestamp\":" + f.Uploaded + ",\"file_name\":\"" + f.FileName + "\"}")) + "],\"file_updates\":[]}";

    [Fact]
    public void Content_search_ignores_other_games_and_broken_entries()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("[" + Hit("107", "5", "a.zip") + "," + Hit("9", "1", "a.zip", "skyrim") + ",{\"mod\":{}}]");
        var hits = NexusClient.ParseMd5Search(doc.RootElement);
        Assert.Equal(("107", "5", "a.zip"), (hits.Single().ModId, hits.Single().FileId, hits.Single().FileName));
    }

    [Fact]
    public void Same_content_in_one_mod_links_without_any_guess()
    {
        var p = Manual("renamed by hand.zip", guess: null);
        var m = NexusLinker.FromContent(p, [new NexusMd5Match("107", "CET", "5", "cet-107-1-0-1700000000.zip", "1.0")]);
        Assert.Equal(("107", "5", LinkEvidence.Content), (m!.ModId, m.FileId, m.Evidence));
    }

    [Fact]
    public void Same_content_in_several_mods_counts_only_for_the_guessed_one()
    {
        NexusMd5Match[] hits = [new("107", "A", "5", "x.zip", null), new("200", "B", "9", "x.zip", null)];
        Assert.Null(NexusLinker.FromContent(Manual("x.zip", guess: null), hits));
        Assert.Null(NexusLinker.FromContent(Manual("x.zip", guess: "300"), hits));
        Assert.Equal("200", NexusLinker.FromContent(Manual("x.zip", guess: "200"), hits)!.ModId);
    }

    [Fact]
    public void Guessed_mod_is_confirmed_by_file_name_or_upload_time_only()
    {
        var files = NexusClient.ParseFiles("3518", System.Text.Json.JsonDocument.Parse(
            Files(("11", "Native Settings UI-3518-1-96-1690000000.zip", 1690000000), ("12", "Native Settings UI-3518-1-97-1700000000.zip", 1700000000))).RootElement);
        // a second download of the same file carries " (1)"
        var byName = NexusLinker.FromFiles(Manual("Native Settings UI-3518-1-97-1700000000 (1).zip", "3518"), files);
        Assert.Equal(("12", LinkEvidence.FileName), (byName!.FileId, byName.Evidence));
        // re-packed under another extension: the upload second in the name still names one file
        var byTime = NexusLinker.FromFiles(Manual("Native Settings UI-3518-1-96-1690000000.7z", "3518"), files);
        Assert.Equal(("11", LinkEvidence.UploadTime), (byTime!.FileId, byTime.Evidence));
        // the name only looks like a Nexus download of this mod
        Assert.Null(NexusLinker.FromFiles(Manual("My Mod-3518-2-0-1234567890.zip", "3518"), files));
        Assert.Null(NexusLinker.FromFiles(Manual("Native Settings UI-3518-1-97-1700000000.zip", "107"), files));
    }

    [Fact]
    public async Task Verify_asks_for_content_first_and_falls_back_to_the_guessed_mod()
    {
        var nexus = new FakeNexus(new()
        {
            [$"md5_search/{Md5}.json"] = (200, $"[{Hit("4885", "77", "Mod Settings-4885-0-2-27-1700000000.zip")}]"),
            ["mods/3518/files.json"] = (200, Files(("12", "Native Settings UI-3518-1-97-1700000000.zip", 1700000000))),
        });
        using var client = new NexusClient("key", "test", nexus);
        var byContent = await NexusLinker.VerifyAsync(client, Manual("whatever.zip", guess: null));
        Assert.Equal(("4885", "77"), (byContent!.ModId, byContent.FileId));
        Assert.Single(nexus.Asked);

        var other = "ffffffffffffffffffffffffffffffff";
        var byName = await NexusLinker.VerifyAsync(client, Manual("Native Settings UI-3518-1-97-1700000000.zip", "3518", other));
        Assert.Equal(("3518", "12", LinkEvidence.FileName), (byName!.ModId, byName.FileId, byName.Evidence));
    }

    [Fact]
    public async Task Verify_links_nothing_without_a_match()
    {
        var nexus = new FakeNexus(new() { ["mods/3518/files.json"] = (200, Files(("12", "Something else-3518-1-0-1600000000.zip", 1600000000))) });
        using var client = new NexusClient("key", "test", nexus);
        Assert.Null(await NexusLinker.VerifyAsync(client, Manual("Native Settings UI-3518-1-97-1700000000.zip", "3518")));
        // the guessed mod does not exist on Nexus, and a package with nothing to ask about sends no request
        Assert.Null(await NexusLinker.VerifyAsync(client, Manual("Gone-999-1-0-1700000000.zip", "999")));
        var before = nexus.Asked.Count;
        Assert.Null(await NexusLinker.VerifyAsync(client, Manual("plain.zip", guess: null, md5: null)));
        Assert.Equal(before, nexus.Asked.Count);
    }

    [Fact]
    public async Task Rate_limit_stops_the_run_instead_of_counting_as_no_match()
    {
        var nexus = new FakeNexus(new() { [$"md5_search/{Md5}.json"] = (429, "{}") });
        using var client = new NexusClient("key", "test", nexus);
        var ex = await Assert.ThrowsAsync<NexusException>(() => NexusLinker.VerifyAsync(client, Manual("a.zip", "107")));
        Assert.True(ex.RateLimited);
        Assert.Single(nexus.Asked); // the fallback is not tried once the limit is reached
    }

    [Fact]
    public void Only_unlinked_mods_added_by_hand_are_candidates()
    {
        var plain = Manual("a.zip", "107", id: "pkg-a");
        var noHint = Manual("b.zip", guess: null, md5: null, id: "pkg-b");
        var linked = Manual("c.zip", "107", id: "pkg-c") with { Origin = PackageOrigin.Nexus };
        var captured = Manual("d.zip", "107", id: "pkg-d") with { Origin = PackageOrigin.Captured };
        var layer = Manual("e.zip", "107", id: "pkg-e") with { LayerOfModId = "mod-x" };
        var fixer = Manual("f.zip", null, id: "pkg-f") with { ModId = "fixer-quest" };
        var core = Manual("g.zip", null, id: "pkg-g") with { Provides = [Capabilities.ArchiveXl] };
        PackageManifest[] all = [plain, noHint, linked, captured, layer, fixer, core];
        Assert.Equal(["pkg-a"], NexusLinker.Candidates(all).Select(p => p.PackageId).ToArray());
        Assert.Equal(["pkg-b"], NexusLinker.WithoutHint(all).Select(p => p.PackageId).ToArray());
    }

    [Fact]
    public void Link_stores_the_file_and_a_miss_is_not_asked_again_for_a_day()
    {
        using var t = new TestEnv();
        var p = t.ImportZip("Z", ("archive/pc/mod/z.archive", "a"));
        Assert.Contains(NexusLinker.Candidates([p]), c => c.PackageId == p.PackageId); // every import keeps the checksum of its archive
        var q = t.C.LinkToNexus(p.PackageId, "107", "5");
        Assert.Equal(("107", "5", PackageOrigin.Nexus), (q.NexusModId, q.NexusFileId, q.Origin));
        Assert.Empty(NexusLinker.Candidates([q]));
        // a link by hand to the same mod keeps the known file, a link to another mod forgets it
        Assert.Equal("5", t.C.LinkToNexus(p.PackageId, "107").NexusFileId);
        Assert.Null(t.C.LinkToNexus(p.PackageId, "200").NexusFileId);

        var now = DateTimeOffset.UtcNow;
        NexusLinker.MarkTried(t.C.Store, "pkg-miss", now.AddHours(-2));
        NexusLinker.MarkTried(t.C.Store, "pkg-old", now.AddHours(-30));
        Assert.Equal(["pkg-miss"], NexusLinker.TriedRecently(t.C.Store, now).ToArray());
    }
}

public class UpdatePlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);
    private static readonly Dictionary<string, long> NothingChanged = new();

    private static UpdateItem Item(string id, DateTimeOffset? checkedAt, DateTimeOffset? fullRead = null, bool core = false, bool active = false, string? modId = null) =>
        new(id, modId ?? "mod" + id, checkedAt, fullRead, core, active);

    private static string[] Ids(IEnumerable<UpdateItem> items) => items.Select(i => i.PackageId).ToArray();

    [Fact]
    public void Between_full_reads_only_the_feed_decides()
    {
        var hourAgo = Now.AddHours(-1);
        var feed = new Dictionary<string, long> { ["modb"] = Now.AddMinutes(-20).ToUnixTimeSeconds(), ["modc"] = Now.AddDays(-3).ToUnixTimeSeconds() };
        UpdateItem[] items = [Item("a", hourAgo, hourAgo), Item("b", hourAgo, hourAgo), Item("c", hourAgo, hourAgo), Item("d", null)];
        // b changed after its check, d was never checked; a and c are covered by the feed
        Assert.Equal(["b", "d"], Ids(UpdatePlan.Due(items, feed, UpdateScope.All, Week, Now)));
    }

    [Fact]
    public void Every_mod_is_read_in_full_once_per_period_even_when_the_feed_is_silent()
    {
        var items = Enumerable.Range(0, 700).Select(n => Item("pkg-" + n, Now.AddMinutes(-30), Now.AddMinutes(-30))).ToList();
        var read = new HashSet<string>();
        var perDay = new List<int>();
        var state = items.ToDictionary(i => i.PackageId);
        for (var day = 1; day <= 7; day++)
        {
            var now = Now.AddDays(day);
            // the hourly check keeps "checked" fresh, as the window does
            var today = UpdatePlan.Due(state.Values.Select(i => i with { CheckedAt = now.AddHours(-1) }), NothingChanged, UpdateScope.All, Week, now);
            perDay.Add(today.Count);
            foreach (var i in today) { read.Add(i.PackageId); state[i.PackageId] = i with { FullReadAt = now }; }
        }
        Assert.Equal(700, read.Count);            // nobody is skipped
        Assert.Equal(700, perDay.Sum());          // and nobody is read twice in the period
        Assert.All(perDay, n => Assert.InRange(n, 60, 140)); // spread over the days, about a seventh each
    }

    [Fact]
    public void A_library_checked_before_full_reads_existed_is_spread_not_read_at_once()
    {
        var items = Enumerable.Range(0, 600).Select(n => Item("pkg-" + n, Now.AddHours(-1))).ToList(); // no full read recorded
        Assert.InRange(UpdatePlan.Due(items, NothingChanged, UpdateScope.All, Week, Now).Count, 0, 20);
        Assert.Empty(UpdatePlan.Due(items, NothingChanged, UpdateScope.All, TimeSpan.Zero, Now)); // 0 days: full reads off
    }

    [Fact]
    public void Scope_limits_the_check_and_core_and_active_mods_come_first()
    {
        UpdateItem[] items = [Item("other", null), Item("active", null, active: true), Item("core", null, core: true), Item("both", null, core: true, active: true)];
        Assert.Equal(["core", "both", "active", "other"], Ids(UpdatePlan.Due(items, NothingChanged, UpdateScope.All, Week, Now)));
        Assert.Equal(["core", "both", "active"], Ids(UpdatePlan.Due(items, NothingChanged, UpdateScope.Active, Week, Now)));
        Assert.Equal(["core", "both"], Ids(UpdatePlan.Due(items, NothingChanged, UpdateScope.CoreOnly, Week, Now)));
    }

    [Fact]
    public void Without_the_feed_a_mod_is_read_once_a_day()
    {
        UpdateItem[] items = [Item("fresh", Now.AddHours(-3), Now.AddHours(-3)), Item("old", Now.AddHours(-30), Now.AddHours(-30))];
        Assert.Contains("old", Ids(UpdatePlan.Due(items, null, UpdateScope.All, TimeSpan.Zero, Now)));
        Assert.DoesNotContain("fresh", Ids(UpdatePlan.Due(items, null, UpdateScope.All, TimeSpan.Zero, Now)));
    }

    [Fact]
    public void Full_reads_are_remembered_per_package()
    {
        using var t = new TestEnv();
        UpdatePlan.MarkFullRead(t.C.Store, "pkg-1", Now);
        Assert.Equal(Now, UpdatePlan.FullReadAll(t.C.Store)["pkg-1"]);
        Assert.Equal(UpdatePlan.Phase("pkg-1", Week), UpdatePlan.Phase("pkg-1", Week)); // the same on every start
    }
}
