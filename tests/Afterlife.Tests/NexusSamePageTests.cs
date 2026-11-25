using Afterlife.Infrastructure;

namespace Afterlife.Tests;

public class NexusSamePageTests
{
    /// <summary>A page with a core file (three versions, the last upload not marked as a replacement), its data file and an add-on.</summary>
    private static NexusFiles Page() => NexusClient.ParseFiles("38654", System.Text.Json.JsonDocument.Parse("""
        {"files":[
          {"file_id":10,"name":"Core","version":"0.2.4","category_name":"OLD_VERSION","uploaded_timestamp":100,"file_name":"core-024.zip"},
          {"file_id":11,"name":"Core","version":"0.3.0","category_name":"OLD_VERSION","uploaded_timestamp":200,"file_name":"core-030.zip"},
          {"file_id":12,"name":"Core","version":"0.3.1","category_name":"MAIN","uploaded_timestamp":300,"file_name":"core-031.zip"},
          {"file_id":20,"name":"Hairstyles","version":"0.1.0","category_name":"OLD_VERSION","uploaded_timestamp":110,"file_name":"hair-010.zip"},
          {"file_id":21,"name":"Hairstyles 0.2","version":"0.2.0","category_name":"MAIN","uploaded_timestamp":210,"file_name":"hair-020.zip"},
          {"file_id":30,"name":"Dev tools","version":"0.1.0","category_name":"OPTIONAL","uploaded_timestamp":120,"file_name":"dev.zip"}],
         "file_updates":[{"old_file_id":10,"new_file_id":11}]}
        """).RootElement);

    private static (SamePageVerdict Verdict, string? PackageId) Decide(string incoming, params (string PackageId, string? FileId)[] installed) =>
        NexusSamePage.Decide(Page(), incoming, installed);

    [Fact]
    public void Another_live_file_of_the_page_stays_a_mod_of_its_own()
    {
        Assert.Equal((SamePageVerdict.Separate, (string?)null), Decide("21", ("core", "12")));
        Assert.Equal((SamePageVerdict.Separate, (string?)null), Decide("12", ("hair", "21")));
        Assert.Equal((SamePageVerdict.Separate, (string?)null), Decide("30", ("core", "12"), ("hair", "21")));
    }

    [Fact]
    public void The_file_Nexus_lists_as_the_next_version_replaces_its_package()
    {
        Assert.Equal((SamePageVerdict.Replace, "core"), Decide("11", ("hair", "21"), ("core", "10")));
    }

    [Fact]
    public void An_unmarked_upload_with_the_same_name_replaces_too()
    {
        Assert.Equal((SamePageVerdict.Replace, "core"), Decide("12", ("hair", "21"), ("core", "11")));
        // the recorded chain ends one version earlier
        Assert.Equal((SamePageVerdict.Replace, "core"), Decide("12", ("core", "10")));
    }

    [Fact]
    public void An_old_file_whose_versions_go_on_elsewhere_is_left_alone()
    {
        Assert.Equal((SamePageVerdict.Separate, (string?)null), Decide("21", ("core", "10")));
    }

    [Fact]
    public void An_old_or_unknown_file_without_a_trace_is_the_users_call()
    {
        // renamed between versions and not marked as a replacement
        Assert.Equal((SamePageVerdict.Unknown, "hair"), Decide("21", ("hair", "20")));
        Assert.Equal((SamePageVerdict.Unknown, "gone"), Decide("21", ("gone", "999")));
        Assert.Equal((SamePageVerdict.Unknown, "nofile"), Decide("21", ("nofile", null)));
        // a certain answer for one package wins over a doubt about another
        Assert.Equal((SamePageVerdict.Replace, "core"), Decide("12", ("nofile", null), ("core", "11")));
    }

    [Fact]
    public void The_same_file_again_is_not_a_version_of_itself()
    {
        Assert.Equal((SamePageVerdict.Separate, (string?)null), Decide("12", ("core", "12")));
    }

    [Fact]
    public void An_old_file_with_a_later_upload_under_its_name_is_left_alone_without_a_question()
    {
        // Core 0.3.0 is in the library, 0.3.1 is on the page unmarked; the data file arrives
        Assert.Equal((SamePageVerdict.Separate, (string?)null), Decide("21", ("core", "11")));
    }

    /// <summary>Two variants of one mod, the second uploaded as "a new version" of the first while both stay on offer.</summary>
    private static NexusFiles Variants(bool firstStillOffered) => NexusClient.ParseFiles("7638", System.Text.Json.JsonDocument.Parse($$"""
        {"files":[
          {"file_id":40,"name":"Tattoo Japanese","version":"1.0","category_name":"{{(firstStillOffered ? "MAIN" : "OLD_VERSION")}}","uploaded_timestamp":100,"file_name":"jp.zip"},
          {"file_id":41,"name":"Tattoo Blackout","version":"1.0","category_name":"MAIN","uploaded_timestamp":200,"file_name":"bo.zip"}],
         "file_updates":[{"old_file_id":40,"new_file_id":41}]}
        """).RootElement);

    [Fact]
    public void A_marked_replacement_of_a_file_still_on_offer_under_another_name_is_never_silent()
    {
        Assert.Equal((SamePageVerdict.Unknown, "jp"), NexusSamePage.Decide(Variants(true), "41", [("jp", "40")]));
        Assert.Equal((SamePageVerdict.Replace, "jp"), NexusSamePage.Decide(Variants(false), "41", [("jp", "40")]));
    }

    [Fact]
    public void Without_a_file_id_several_main_files_offer_no_update()
    {
        var now = DateTimeOffset.UtcNow;
        var twoMains = NexusClient.ParseFiles("7638", System.Text.Json.JsonDocument.Parse("""
            {"files":[
              {"file_id":40,"name":"Tattoo Japanese","version":"1.0","category_name":"MAIN","uploaded_timestamp":100,"file_name":"jp.zip"},
              {"file_id":41,"name":"Tattoo Blackout","version":"2.0","category_name":"MAIN","uploaded_timestamp":200,"file_name":"bo.zip"}],
             "file_updates":[]}
            """).RootElement);
        Assert.Null(NexusUpdates.Find(twoMains, null, "1.0", now));
        // one line of versions, both still listed as main: the newest is the update
        Assert.Equal("41", NexusUpdates.Find(Variants(true), null, "0.9", now)!.FileId);
    }
}
