using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;


public class CollectionTests
{
    [Theory]
    [InlineData("nxm://cyberpunk2077/collections/rcuccp/revisions/latest", "rcuccp", null)]
    [InlineData("nxm://cyberpunk2077/collections/g14kxi/revisions/2", "g14kxi", 2)]
    [InlineData("https://www.nexusmods.com/games/cyberpunk2077/collections/abc123", "abc123", null)]
    [InlineData("https://next.nexusmods.com/cyberpunk2077/collections/abc123/revisions/7", "abc123", 7)]
    public void Parses_collection_links(string url, string slug, int? rev)
    {
        Assert.True(CollectionLink.TryParse(url, out var l));
        Assert.Equal(slug, l.Slug);
        Assert.Equal(rev, l.Revision);
        Assert.Equal("cyberpunk2077", l.Game);
        Assert.False(CollectionLink.TryParse("nxm://cyberpunk2077/mods/107/files/1", out _));
    }

    internal static CollectionRevisionInfo Api() => CollectionLogic.FromGraphQl("abc", System.Text.Json.JsonDocument.Parse("""
        {"data":{"collectionRevision":{"revisionNumber":3,"downloadLink":"/v1/collections/abc/revisions/3/download_link","installationInfo":"Run the game once.",
          "adultContent":false,"totalSize":1000,
          "collection":{"name":"Night City Plus","summary":"s","slug":"abc","user":{"name":"Curator"},"latestPublishedRevision":{"revisionNumber":4}},
          "modFiles":[
            {"fileId":11,"optional":false,"version":"1.0","updatePolicy":"prefer","file":{"fileId":11,"name":"A main","version":"1.0","modId":101,"sizeInBytes":10,"mod":{"name":"Mod A"}}},
            {"fileId":22,"optional":false,"version":"2.0","updatePolicy":"prefer","file":{"fileId":22,"name":"B main","version":"2.0","modId":202,"sizeInBytes":10,"mod":{"name":"Mod B"}}},
            {"fileId":33,"optional":true,"version":"3.0","updatePolicy":"prefer","file":{"fileId":33,"name":"C","version":"3.0","modId":303,"sizeInBytes":10,"mod":{"name":"Mod C"}}}],
          "externalResources":[{"name":"Guide tool","version":"1","optional":true,"resourceUrl":"https://example.com","instructions":"by hand"}]}}}
        """).RootElement);

    [Fact]
    public void Reads_the_revision_from_the_api()
    {
        var r = Api();
        Assert.Equal("Night City Plus", r.Name);
        Assert.Equal(3, r.Revision);
        Assert.Equal(4, r.LatestRevision);
        Assert.Equal(4, r.Items.Count);
        Assert.True(r.Items.Single(i => i.Name == "Mod C").Optional);
        Assert.Equal(CollectionSource.Browse, r.Items.Single(i => i.Name == "Guide tool").Source);
    }

    [Fact]
    public void Manifest_adds_installer_choices_and_rules()
    {
        var json = """
            {"info":{"name":"Night City Plus"},
             "mods":[
               {"name":"Mod A","version":"1.0","optional":false,"source":{"type":"nexus","modId":101,"fileId":11,"md5":"aaa","logicalFilename":"A main"},
                "choices":{"type":"fomod","options":[{"name":"Main","groups":[{"name":"Color","choices":[{"name":"Red","idx":0}]}]}]}},
               {"name":"Mod B","version":"2.0","optional":false,"source":{"type":"nexus","modId":202,"fileId":22,"md5":"bbb","logicalFilename":"B main"}},
               {"name":"Bundled tweak","version":"1","optional":false,"source":{"type":"bundle","logicalFilename":"tweak"}}],
             "modRules":[{"type":"before","source":{"fileMD5":"bbb"},"reference":{"fileMD5":"aaa"}}]}
            """;
        var r = CollectionLogic.FromManifest(Api(), json);
        Assert.True(r.FromBundle);
        var a = r.Items.Single(i => i.Name == "Mod A");
        Assert.Equal("Red", a.Choices![0].Plugins[0]);
        Assert.Equal(CollectionSource.Bundle, r.Items.Single(i => i.Name == "Bundled tweak").Source);
        var rule = Assert.Single(r.Rules);
        Assert.Equal(("before", "file:22", "file:11"), (rule.Type, rule.From, rule.To));
    }

    [Fact]
    public void Order_follows_the_collection_rules_and_modlist()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("Mod A", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("Mod B", ("archive/pc/mod/b.archive", "b"));
        var api = Api();
        var rev = api with { Rules = [new CollectionRule("before", "file:22", "file:11")] };
        var rec = new CollectionRecord(rev, null, new Dictionary<string, string> { ["file:11"] = a.PackageId, ["file:22"] = b.PackageId }, [], [], true, DateTimeOffset.UtcNow);
        Assert.Equal([b.PackageId, a.PackageId], CollectionLogic.Order(rec, t.C.Store.GetPackages()));
        var withList = rec with { Revision = rev with { Rules = [], ArchiveOrder = ["a.archive", "b.archive"] } };
        Assert.Equal([a.PackageId, b.PackageId], CollectionLogic.Order(withList, t.C.Store.GetPackages()));
    }

    [Fact]
    public void Removal_keeps_what_others_need()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/version.dll", "v"));
        var onlyHere = t.ImportZip("Mod A", ("archive/pc/mod/a.archive", "a"));
        var shared = t.ImportZip("Mod B", ("archive/pc/mod/b.archive", "b"));
        var mine = t.ImportZip("My Lua", ("bin/x64/plugins/cyber_engine_tweaks/mods/Lua/init.lua", "x"));
        foreach (var p in new[] { cet, onlyHere, shared, mine }) t.C.SetEnabled(p.PackageId, true);
        var api = Api();
        var r1 = new CollectionRecord(api, null, new Dictionary<string, string> { ["file:11"] = onlyHere.PackageId, ["file:22"] = shared.PackageId, ["file:99"] = cet.PackageId }, [], [], true, DateTimeOffset.UtcNow);
        var r2 = new CollectionRecord(api with { Slug = "other", Name = "Other" }, null, new Dictionary<string, string> { ["file:22"] = shared.PackageId }, [], [], true, DateTimeOffset.UtcNow);
        var o = OverviewBuilder.Build(t.C);
        var (remove, keep) = CollectionLogic.RemovalPlan(r1, [r1, r2], o);
        Assert.Equal([onlyHere.PackageId], remove.Select(x => x.PackageId).ToArray());
        Assert.Contains(keep, k => k.PackageId == shared.PackageId && k.KeptBecause!.Contains("Other"));
        Assert.Contains(keep, k => k.PackageId == cet.PackageId && k.KeptBecause!.Contains("My Lua"));
    }

    [Fact]
    public void Diff_between_revisions()
    {
        var old = Api();
        var items = old.Items.Where(i => i.Name != "Mod B").Select(i => i.Name == "Mod A" ? i with { FileId = "12", Version = "1.1" } : i).ToList();
        items.Add(new CollectionItem("Mod D", "1", false, CollectionSource.Nexus, "404", "44", null, null, null, null, null, null, 0, null));
        var d = CollectionLogic.Diff(old, old with { Items = items, Revision = 4 });
        Assert.Equal("Mod D", Assert.Single(d.Added).Name);
        Assert.Equal("Mod B", Assert.Single(d.Removed).Name);
        Assert.Equal("1.1", Assert.Single(d.Updated).New.Version);
    }

    [Fact]
    public void Records_are_stored_and_listed()
    {
        using var t = new TestEnv();
        var rec = new CollectionRecord(Api(), "p1", new Dictionary<string, string>(), [], [], true, DateTimeOffset.UtcNow);
        CollectionStore.Save(t.C.Store, rec);
        Assert.Equal("Night City Plus", Assert.Single(t.C.Collections()).Name);
        Assert.Equal(2, rec.Missing.Count); // required A and B; optional C and the external tool not chosen
        CollectionStore.Delete(t.C.Store, "abc");
        Assert.Empty(t.C.Collections());
    }

    [Fact]
    public void Collection_uses_a_core_mod_already_in_the_library()
    {
        using var t = new TestEnv();
        var cet = t.ImportZip("CET", ("bin/x64/plugins/cyber_engine_tweaks.asi", "asi"), ("bin/x64/version.dll", "v"));
        var withId = cet with { NexusModId = "107", NexusFileId = "1" };
        t.C.Store.UpdatePackage(withId);
        var item = new CollectionItem("Cyber Engine Tweaks", "1.38", false, CollectionSource.Nexus, "107", "999", null, null, null, null, null, null, 0, null);
        var rev = new CollectionRevisionInfo("abc", "Test", null, 1, null, null, null, false, 0, null, [item], []);
        var s = Assert.Single(CollectionLogic.States(rev, t.C.Store.GetPackages()));
        Assert.Equal(ItemStatus.InLibrary, s.Status);
        Assert.Equal(cet.PackageId, s.PackageId);
    }
}
