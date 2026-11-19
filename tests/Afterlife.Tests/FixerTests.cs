using System.Text.Json;
using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

/// <summary>The drop box for FIXER: packages and removal requests in fixer/inbox, answers in fixer/outbox.</summary>
public class FixerTests
{
    private static string Manifest(string id, string version = "1", string kind = "quest", int schema = 1, params string[] requires) =>
        JsonSerializer.Serialize(new { schema, kind, id, name = "Quest " + id, version, author = "fixer", requires });

    private static TestEnv Open()
    {
        var t = new TestEnv();
        t.C.Fixer.SetEnabled(true, "full", "test");
        return t;
    }

    /// <summary>Puts a zip into the inbox the way FIXER does; returns its path there.</summary>
    private static string Drop(TestEnv t, string name, params (string Path, string Content)[] files)
    {
        var inbox = t.C.Fixer.Paths.Inbox;
        Directory.CreateDirectory(inbox);
        var dest = Path.Combine(inbox, name + ".zip");
        File.Move(t.Zip(name, files), dest, overwrite: true);
        return dest;
    }

    private static void DropRemoval(TestEnv t, string name, string id) =>
        File.WriteAllText(Path.Combine(t.C.Fixer.Paths.Inbox, name + ".json"), JsonSerializer.Serialize(new { schema = 1, kind = "remove", id }));

    private static JsonElement ResultOf(TestEnv t, string file) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(t.C.Fixer.Paths.Outbox, file + ".result.json"))).RootElement;

    private static string StatusOf(TestEnv t, string file) => ResultOf(t, file).GetProperty("status").GetString()!;

    [Fact]
    public void Off_by_default_and_the_inbox_is_not_read()
    {
        using var t = new TestEnv();
        Assert.False(t.C.Fixer.Enabled);
        t.C.Fixer.Announce("full", "test");
        Assert.False(Directory.Exists(t.C.Fixer.Paths.Root)); // a library that never used it gets no folder

        var zip = Drop(t, "q-1", ("fixer.json", Manifest("q")), ("archive/pc/mod/q.archive", "a"));
        Assert.Empty(t.C.Fixer.Process());
        Assert.True(File.Exists(zip));
        Assert.Empty(t.C.Store.GetPackages());
    }

    [Fact]
    public void Status_file_says_whether_the_drop_box_is_on()
    {
        using var t = new TestEnv();
        t.C.Fixer.SetEnabled(true, "offline", "1.2.3");
        var s = JsonDocument.Parse(File.ReadAllText(t.C.Fixer.Paths.Status)).RootElement;
        Assert.Equal(1, s.GetProperty("schema").GetInt32());
        Assert.True(s.GetProperty("enabled").GetBoolean());
        Assert.Equal("AFTERLIFE", s.GetProperty("app").GetString());
        Assert.Equal("offline", s.GetProperty("edition").GetString());
        Assert.Equal("1.2.3", s.GetProperty("version").GetString());
        Assert.True(Directory.Exists(t.C.Fixer.Paths.Inbox));

        t.Restart();
        Assert.True(t.C.Fixer.Enabled);
        t.C.Fixer.SetEnabled(false, "offline", "1.2.3");
        s = JsonDocument.Parse(File.ReadAllText(t.C.Fixer.Paths.Status)).RootElement;
        Assert.False(s.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Package_goes_to_the_library_the_selected_profile_and_the_category()
    {
        using var t = Open();
        Drop(t, "q-1",
            ("fixer.json", Manifest("q", "2026-11-20T1015", "quest", 1, Capabilities.Codeware, "framework:nope")),
            ("archive/pc/mod/q.archive", "a"), ("archive/pc/mod/q.archive.xl", "x"), ("r6/scripts/q/q.reds", "func f() {}"));
        var r = Assert.Single(t.C.Fixer.Process());
        Assert.Equal(FixerResult.Imported, r.Status);
        Assert.Contains("framework:nope", r.Message);

        var p = Assert.Single(t.C.Store.GetPackages());
        Assert.Equal("fixer-q", p.ModId);
        Assert.Equal("Quest q", p.ModName);
        Assert.Equal("2026-11-20T1015", p.Version);
        Assert.Equal(3, p.Files.Count); // fixer.json is not a game file
        Assert.True(t.C.SelectedProfile.IsEnabled(p.PackageId));
        Assert.Contains(p.Requirements, x => x.Capability == Capabilities.Codeware && x.Origin == FixerService.RequirementOrigin);

        var layout = BlockLayout.FromJson(t.C.Store.GetMeta(BlockLayout.StoreKey));
        var category = layout.CategoryIdOf(p.PackageId);
        Assert.NotNull(category);
        Assert.Equal(FixerService.CategoryName, layout.Categories.Single(c => c.Id == category).Name);

        Assert.Empty(Directory.GetFiles(t.C.Fixer.Paths.Inbox));
        Assert.True(File.Exists(Path.Combine(t.C.Fixer.Paths.Done, "q-1.zip")));
        var json = ResultOf(t, "q-1.zip");
        Assert.Equal(1, json.GetProperty("schema").GetInt32());
        Assert.Equal("q-1.zip", json.GetProperty("file").GetString());
        Assert.Equal("q", json.GetProperty("id").GetString());
        Assert.Equal("imported", json.GetProperty("status").GetString());
        Assert.Equal(p.PackageId, json.GetProperty("packageId").GetString());

        // what FIXER listed stays when the requirements are derived from the files again
        t.C.RefreshRequirements();
        var again = t.C.Store.GetPackage(p.PackageId)!.Requirements;
        Assert.Contains(again, x => x.Capability == Capabilities.Codeware && x.Origin == FixerService.RequirementOrigin);
        Assert.Contains(again, x => x.Capability == Capabilities.Redscript);
        Assert.Contains(again, x => x.Capability == Capabilities.ArchiveXl);
    }

    [Fact]
    public void A_file_still_being_written_waits()
    {
        using var t = Open();
        var zip = Drop(t, "q-1", ("fixer.json", Manifest("q")), ("archive/pc/mod/q.archive", "a"));
        File.Move(zip, zip + ".part");
        Assert.False(FixerService.HasPending(t.LibraryRoot));
        Assert.Empty(t.C.Fixer.Process());

        File.Move(zip + ".part", zip);
        Assert.True(FixerService.HasPending(t.LibraryRoot));
        Assert.Single(t.C.Fixer.Process());
        Assert.False(FixerService.HasPending(t.LibraryRoot));
        Assert.Empty(t.C.Fixer.Process());
    }

    [Fact]
    public void A_new_build_takes_the_place_of_the_old_one()
    {
        using var t = Open();
        Drop(t, "q-1", ("fixer.json", Manifest("q", "1")), ("archive/pc/mod/q.archive", "a"));
        t.C.Fixer.Process();
        var first = Assert.Single(t.C.Store.GetPackages());
        var photo = t.C.CreateProfile("Photo");
        t.C.SetEnabledMany(photo.Id, [first.PackageId], true);

        Drop(t, "q-2", ("fixer.json", Manifest("q", "2")), ("archive/pc/mod/q.archive", "b"));
        var r = Assert.Single(t.C.Fixer.Process());
        var second = Assert.Single(t.C.Store.GetPackages()); // the old build never reached the game: gone at once
        Assert.NotEqual(first.PackageId, second.PackageId);
        Assert.Equal(second.PackageId, r.PackageId);
        Assert.Equal("2", second.Version);
        Assert.True(t.C.SelectedProfile.IsEnabled(second.PackageId));
        Assert.True(t.C.Store.GetProfile(photo.Id)!.IsEnabled(second.PackageId));
        Assert.NotNull(BlockLayout.FromJson(t.C.Store.GetMeta(BlockLayout.StoreKey)).CategoryIdOf(second.PackageId));
    }

    [Fact]
    public void Apply_puts_the_quest_in_the_game_and_the_result_says_so()
    {
        using var t = Open();
        Drop(t, "q-1", ("fixer.json", Manifest("q", "1")), ("archive/pc/mod/q.archive", "a"));
        t.C.Fixer.Process();
        Assert.Equal("imported", StatusOf(t, "q-1.zip"));
        t.ApplyOk();
        t.C.Fixer.AfterApply();
        Assert.Equal("a", t.Read("archive/pc/mod/q.archive"));
        Assert.Equal("applied", StatusOf(t, "q-1.zip"));

        Drop(t, "q-2", ("fixer.json", Manifest("q", "2")), ("archive/pc/mod/q.archive", "b"));
        t.C.Fixer.Process();
        Assert.Equal(2, t.C.Store.GetPackages().Count); // the old build is still in the game
        Assert.Equal("imported", StatusOf(t, "q-2.zip"));
        Assert.Equal("a", t.Read("archive/pc/mod/q.archive"));

        t.ApplyOk();
        t.C.Fixer.AfterApply();
        Assert.Equal("b", t.Read("archive/pc/mod/q.archive"));
        Assert.Equal("2", Assert.Single(t.C.Store.GetPackages()).Version);
        Assert.Equal("applied", StatusOf(t, "q-2.zip"));
    }

    [Fact]
    public void Removal_switches_the_quest_off_everywhere_and_apply_takes_it_out()
    {
        using var t = Open();
        Drop(t, "q-1", ("fixer.json", Manifest("q", "1")), ("archive/pc/mod/q.archive", "a"));
        t.C.Fixer.Process();
        var p = Assert.Single(t.C.Store.GetPackages());
        var photo = t.C.CreateProfile("Photo");
        t.C.SetEnabledMany(photo.Id, [p.PackageId], true);
        t.ApplyOk();

        DropRemoval(t, "remove-q-1", "q");
        var r = Assert.Single(t.C.Fixer.Process());
        Assert.Equal(FixerResult.Removed, r.Status);
        Assert.Contains("APPLY CHANGES", r.Message);
        Assert.False(t.C.SelectedProfile.IsEnabled(p.PackageId));
        Assert.False(t.C.Store.GetProfile(photo.Id)!.IsEnabled(p.PackageId));
        Assert.Single(t.C.Store.GetPackages()); // stays in the library
        Assert.Equal("a", t.Read("archive/pc/mod/q.archive"));
        t.ApplyOk();
        Assert.Null(t.Read("archive/pc/mod/q.archive"));

        // a quest AFTERLIFE never had is not on either: FIXER may forget it
        DropRemoval(t, "remove-zzz-1", "zzz");
        r = Assert.Single(t.C.Fixer.Process());
        Assert.Equal(FixerResult.Removed, r.Status);
        Assert.Null(r.PackageId);

        // the same build sent again is the same package, switched on again
        Drop(t, "q-2", ("fixer.json", Manifest("q", "1")), ("archive/pc/mod/q.archive", "a"));
        r = Assert.Single(t.C.Fixer.Process());
        Assert.Equal(FixerResult.Imported, r.Status);
        Assert.Equal(p.PackageId, Assert.Single(t.C.Store.GetPackages()).PackageId);
        Assert.True(t.C.SelectedProfile.IsEnabled(p.PackageId));
    }

    [Fact]
    public void What_cannot_be_read_is_rejected_and_leaves_the_inbox()
    {
        using var t = Open();
        Drop(t, "nomanifest", ("archive/pc/mod/a.archive", "a"));
        Drop(t, "schema2", ("fixer.json", Manifest("q", "1", "quest", 2)), ("archive/pc/mod/a.archive", "a"));
        Drop(t, "badid", ("fixer.json", Manifest("Bad Id")), ("archive/pc/mod/a.archive", "a"));
        Drop(t, "badkind", ("fixer.json", Manifest("q", "1", "remove")), ("archive/pc/mod/a.archive", "a"));
        Drop(t, "nofiles", ("fixer.json", Manifest("empty")), ("readme.txt", "hi"));
        File.WriteAllText(Path.Combine(t.C.Fixer.Paths.Inbox, "remove-broken.json"), "{ not json");

        var results = t.C.Fixer.Process();
        Assert.Equal(6, results.Count);
        Assert.All(results, r => Assert.Equal(FixerResult.Rejected, r.Status));
        Assert.Empty(t.C.Store.GetPackages());
        Assert.Empty(Directory.GetFiles(t.C.Fixer.Paths.Inbox));
        Assert.Equal(6, Directory.GetFiles(t.C.Fixer.Paths.Done).Length);
        Assert.Contains("schema 2", ResultOf(t, "schema2.zip").GetProperty("message").GetString());
        Assert.Equal("rejected", StatusOf(t, "remove-broken.json"));
    }

    [Fact]
    public void With_mods_off_selected_the_quest_only_waits_in_the_library()
    {
        using var t = Open();
        t.C.SelectProfile(ProfileState.ModsOffId);
        Drop(t, "q-1", ("fixer.json", Manifest("q")), ("archive/pc/mod/q.archive", "a"));
        var r = Assert.Single(t.C.Fixer.Process());
        Assert.Equal(FixerResult.Imported, r.Status);
        Assert.Contains("Select a profile", r.Message);
        var p = Assert.Single(t.C.Store.GetPackages());
        Assert.False(t.C.Store.GetProfile("street")!.IsEnabled(p.PackageId));
    }
}
