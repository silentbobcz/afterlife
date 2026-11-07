using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;


public class BulkTests
{
    [Fact]
    public void Enable_many_in_another_profile_and_delete_reports_what_must_stay()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("B", ("archive/pc/mod/b.archive", "b"));
        var photo = t.C.CreateProfile("Photo");
        t.C.SetEnabledMany(photo.Id, [a.PackageId, b.PackageId], true);
        Assert.True(t.C.Store.GetProfile(photo.Id)!.IsEnabled(b.PackageId));
        Assert.False(t.C.SelectedProfile.IsEnabled(a.PackageId));
        Assert.Throws<InvalidOperationException>(() => t.C.SetEnabledMany(ProfileState.ModsOffId, [a.PackageId], true));

        t.C.SelectProfile(photo.Id);
        t.ApplyOk();
        var (deleted, kept) = t.C.DeleteFromLibrary([a.PackageId, b.PackageId]);
        Assert.Empty(deleted);
        Assert.Equal(2, kept.Count);
        Assert.False(t.C.Store.GetProfile(photo.Id)!.IsEnabled(a.PackageId)); // switched off everywhere
        Assert.Equal("a", t.Read("archive/pc/mod/a.archive"));                  // still in game until apply
    }

    [Fact]
    public void Never_deployed_packages_are_deleted_at_once()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("B", ("archive/pc/mod/b.archive", "b"));
        t.C.SetEnabledMany("street", [a.PackageId, b.PackageId], true);
        var (deleted, kept) = t.C.DeleteFromLibrary([a.PackageId, b.PackageId]);
        Assert.Equal(2, deleted.Count);
        Assert.Empty(kept);
        Assert.Empty(t.C.Store.GetPackages());
    }
}

/// <summary>Deleting from the library and from the game at once.</summary>
public class DeleteTests
{
    [Fact]
    public void Delete_everywhere_takes_mods_out_of_the_game_and_the_library()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("B", ("archive/pc/mod/b.archive", "b"));
        var c = t.ImportZip("C", ("archive/pc/mod/c.archive", "c"));
        t.C.SetEnabledMany("street", [a.PackageId, b.PackageId, c.PackageId], true);
        var photo = t.C.CreateProfile("Photo");
        t.C.SetEnabledMany(photo.Id, [a.PackageId], true);
        t.ApplyOk();
        // an older generation used to keep packages ("kept for returning to a recent deployment")
        t.C.SetEnabled(c.PackageId, false);
        t.ApplyOk();
        t.C.SetEnabled(c.PackageId, true); // pending edit that must stay pending

        var r = t.C.DeleteEverywhere([a.PackageId, b.PackageId]);
        Assert.Equal(2, r.Deleted.Count);
        Assert.Empty(r.Kept);
        Assert.NotNull(r.Execution);
        Assert.False(File.Exists(Path.Combine(t.Game, "archive", "pc", "mod", "a.archive")));
        Assert.False(File.Exists(Path.Combine(t.Game, "archive", "pc", "mod", "b.archive")));
        Assert.Single(t.C.Store.GetPackages());
        Assert.DoesNotContain(t.C.Store.GetProfile(photo.Id)!.Entries, e => e.PackageId == a.PackageId);
        Assert.True(t.C.SelectedProfile.IsEnabled(c.PackageId));
        Assert.False(File.Exists(Path.Combine(t.Game, "archive", "pc", "mod", "c.archive"))); // not applied by the delete
    }

    [Fact]
    public void Packages_only_in_older_generations_can_be_deleted()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        t.C.SetEnabled(a.PackageId, false);
        t.ApplyOk();
        var (deleted, kept) = t.C.DeleteFromLibrary([a.PackageId]);
        Assert.Single(deleted);
        Assert.Empty(kept);
    }

    [Fact]
    public void Deleting_a_mod_that_changed_its_own_settings_keeps_the_copy_in_removed()
    {
        using var t = new TestEnv();
        var m = t.ImportZip("Truck", ("archive/pc/mod/truck.archive", "x"), ("archive/pc/mod/truck_settings.json", "{}"));
        t.C.SetEnabled(m.PackageId, true);
        t.ApplyOk();
        var settings = Path.Combine(t.Game, "archive", "pc", "mod", "truck_settings.json");
        File.SetAttributes(settings, FileAttributes.Normal);
        File.WriteAllText(settings, "{\"speed\":2}");

        var r = t.C.DeleteEverywhere([m.PackageId]);
        Assert.Single(r.Deleted);
        Assert.Empty(r.Kept);
        Assert.False(File.Exists(settings));
        Assert.Contains(t.C.Removed.List(), b => b.Label.Contains("Changed files of Truck"));
    }
}

public class LoadOrderTests
{
    [Fact]
    public void Reorder_sets_entry_order_and_modlist()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("Alpha", ("archive/pc/mod/a.archive", "a"));
        var b = t.ImportZip("Beta", ("archive/pc/mod/b.archive", "b"));
        t.C.SetEnabledMany("street", [a.PackageId, b.PackageId], true);
        var p = t.C.Reorder("street", [b.PackageId, a.PackageId]);
        Assert.True(p.ArchiveOrder.Custom);
        Assert.Equal(["b.archive", "a.archive"], p.ArchiveOrder.Order);
        Assert.Equal(b.PackageId, p.Entries[0].PackageId);
        t.ApplyOk();
        var modlist = File.ReadAllLines(Path.Combine(t.Game, "archive", "pc", "mod", "modlist.txt"));
        Assert.True(Array.IndexOf(modlist, "b.archive") < Array.IndexOf(modlist, "a.archive"));
        var o = OverviewBuilder.Build(t.C);
        Assert.True(o.OrderIsCustom);
        Assert.Equal("Beta", o.Mods[0].Package.ModName);
        var q = t.C.ResetOrder("street");
        Assert.False(q.ArchiveOrder.Custom);
        Assert.Throws<InvalidOperationException>(() => t.C.Reorder(ProfileState.ModsOffId, [a.PackageId]));
    }
}

public class ProfileTests
{
    [Fact]
    public void Profiles_can_be_deleted_but_not_the_one_in_the_game()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        var street = t.C.SelectedProfile.Id;
        var photo = t.C.CreateProfile("Photo", street);
        Assert.Throws<InvalidOperationException>(() => t.C.DeleteProfile(street));            // in the game now
        Assert.Throws<InvalidOperationException>(() => t.C.DeleteProfile(ProfileState.ModsOffId));
        t.C.SelectProfile(photo.Id);
        t.C.DeleteProfile(photo.Id);
        Assert.Null(t.C.Store.GetProfile(photo.Id));
        Assert.Equal(street, t.C.SelectedProfile.Id);                                         // back to the deployed one
        Assert.NotNull(t.C.Store.GetPackage(a.PackageId));                                     // mods stay
    }

    [Fact]
    public void Profiles_can_be_renamed_but_not_to_an_existing_name()
    {
        using var t = new TestEnv();
        var p = t.C.CreateProfile("Main");
        t.C.CreateProfile("Test");
        Assert.Equal("Story run", t.C.RenameProfile(p.Id, "  Story run ").Name);
        Assert.Equal("Story run", t.C.Store.GetProfile(p.Id)!.Name);
        Assert.Throws<InvalidOperationException>(() => t.C.RenameProfile(p.Id, "test"));
        Assert.Throws<InvalidOperationException>(() => t.C.RenameProfile(ProfileState.ModsOffId, "Other"));
    }
}

public class ModNameTests
{
    [Fact]
    public void Mods_can_be_renamed_and_named_back()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("Some mod-1234-1-0-1700000000", ("archive/pc/mod/a.archive", "a"));
        var original = a.ModName;
        Assert.Equal("Truck sim", t.C.RenamePackage(a.PackageId, "  Truck sim ").ModName);
        Assert.Equal("Truck sim", t.C.Store.GetPackage(a.PackageId)!.ModName);
        Assert.Equal("Other", t.C.RenamePackage(a.PackageId, "Other").ModName);
        Assert.Equal(original, t.C.RenamePackage(a.PackageId, "").ModName);   // empty = original name
    }
}

public class VersionTests
{
    [Theory]
    [InlineData("2.50", "2.5")]
    [InlineData("2.31", "2.31")]
    [InlineData("2.3", "2.3")]
    [InlineData("2.21", "2.21")]
    [InlineData("3.0.88.46235", null)]
    [InlineData("", null)]
    public void Game_patch_from_product_version(string product, string? expected) => Assert.Equal(expected, VersionText.GamePatch(product));

    [Theory]
    [InlineData("1.37.1.0", "1.37.1")]
    [InlineData("v1.31.0+abc", "1.31.0")]
    [InlineData("0.0.0.0", null)]
    [InlineData("1.28", "1.28")]
    public void Clean_framework_version(string raw, string? expected) => Assert.Equal(expected, VersionText.Clean(raw));

    [Fact]
    public void Old_version_text_does_not_raise_a_false_build_warning()
    {
        var old = new GameFingerprint("3.0.5813403", 100, 42);
        var now = new GameFingerprint("3.0.88.46235", 100, 42, "2.5");
        Assert.True(old.SameExecutable(now));
        Assert.Equal("2.5 · build 3.0.88.46235", now.Display);
    }
}

/// <summary>The same files are never imported twice; older duplicates can be merged.</summary>
public class DuplicateTests
{
    [Fact]
    public void The_same_files_imported_again_give_the_existing_package()
    {
        using var t = new TestEnv();
        var first = t.C.Import(new ImportRequest(t.Zip("Black Chrome", ("archive/pc/mod/bc.archive", "chrome")), "Black Chrome", "1.1.150")).Package!;
        var again = t.C.Import(new ImportRequest(t.Zip("Black Chrome copy", ("archive/pc/mod/bc.archive", "chrome")), "Black Chrome", "1.1.150"));
        Assert.True(again.Succeeded);
        Assert.True(again.AlreadyInLibrary);
        Assert.Equal(first.PackageId, again.Package!.PackageId);
        Assert.Single(t.C.Store.GetPackages());
        var other = t.C.Import(new ImportRequest(t.Zip("Black Chrome 2", ("archive/pc/mod/bc.archive", "chrome 2")), "Black Chrome", "1.2"));
        Assert.False(other.AlreadyInLibrary);
        Assert.Equal(2, t.C.Store.GetPackages().Count);
    }

    [Fact]
    public void Duplicates_are_merged_into_one_package()
    {
        using var t = new TestEnv();
        var a = t.C.Import(new ImportRequest(t.Zip("A", ("archive/pc/mod/a.archive", "same")), "A", "1")).Package!;
        // a duplicate made before 0.5.3: store a second record with the same files
        var dup = a with { PackageId = "pkg-duplicate01", ImportedAt = DateTimeOffset.UtcNow.AddMinutes(1) };
        t.C.Store.AddPackage(dup);
        t.C.SetEnabled(dup.PackageId, true);
        var rec = new CollectionRecord(CollectionTests.Api(), null, new Dictionary<string, string> { ["file:11"] = dup.PackageId }, [], [], true, DateTimeOffset.UtcNow);
        CollectionStore.Save(t.C.Store, rec);

        var group = Assert.Single(t.C.DuplicateGroups());
        Assert.Equal(2, group.Count);
        t.C.AdoptDuplicates(a.PackageId, [dup.PackageId]);
        Assert.True(t.C.SelectedProfile.IsEnabled(a.PackageId));
        Assert.DoesNotContain(t.C.SelectedProfile.Entries, e => e.PackageId == dup.PackageId);
        Assert.Equal(a.PackageId, t.C.Collections().Single().PackageOf["file:11"]);
        t.C.DeleteEverywhere([dup.PackageId]);
        Assert.Empty(t.C.DuplicateGroups());
    }
}

public class ReplaceVersionTests
{
    [Fact]
    public void New_version_takes_the_place_of_the_old_one_and_the_old_one_can_be_deleted_after_apply()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/x.archive", "A"));
        var b = t.ImportZip("B", ("archive/pc/mod/x.archive", "B"));
        var old = t.ImportZip("Mod", ("archive/pc/mod/mod.archive", "v1"));
        t.C.UpdateSelected(p => p.WithEntry(a.PackageId, true).WithEntry(b.PackageId, true).WithEntry(old.PackageId, true)
            .WithWinner("ARCHIVE/PC/MOD/X.ARCHIVE", a.PackageId));
        t.ApplyOk();
        var neu = t.C.Import(new ImportRequest(t.Zip("Mod2", ("archive/pc/mod/mod.archive", "v2")), "Mod", "2.0", ExistingModId: old.ModId)).Package!;
        // winner rule pointing at the old version is carried over too
        t.C.UpdateSelected(p => p.WithWinner("ARCHIVE/PC/MOD/X.ARCHIVE", old.PackageId));

        t.C.ReplaceVersion(old.PackageId, neu.PackageId);
        var p = t.C.SelectedProfile;
        Assert.DoesNotContain(p.Entries, e => e.PackageId == old.PackageId);
        Assert.True(p.IsEnabled(neu.PackageId));
        Assert.Equal(neu.PackageId, p.PathWinners["ARCHIVE/PC/MOD/X.ARCHIVE"]);

        t.ApplyOk();
        Assert.Equal("v2", t.Read("archive/pc/mod/mod.archive"));
        var (deleted, kept) = t.C.DeleteFromLibrary([old.PackageId]);
        Assert.Single(deleted);
        Assert.Empty(kept);
        Assert.Null(t.C.Store.GetPackage(old.PackageId));
        Assert.Equal("v2", t.Read("archive/pc/mod/mod.archive"));
    }

    [Fact]
    public void A_new_version_takes_the_place_of_the_old_one_in_collections()
    {
        using var t = new TestEnv();
        var v1 = t.C.Import(new ImportRequest(t.Zip("Mod-1", ("archive/pc/mod/m.archive", "v1")), "Mod", "1.0")).Package!;
        var v2 = t.C.Import(new ImportRequest(t.Zip("Mod-2", ("archive/pc/mod/m.archive", "v2")), "Mod", "2.0", ExistingModId: v1.ModId)).Package!;
        CollectionStore.Save(t.C.Store, new CollectionRecord(CollectionTests.Api(), null, new Dictionary<string, string> { ["file:11"] = v1.PackageId }, [], [], true, DateTimeOffset.UtcNow));
        t.C.ReplaceInCollections(v1.PackageId, v2.PackageId);
        Assert.Equal(v2.PackageId, t.C.Collections().Single().PackageOf["file:11"]);
    }
}

public class ExportTests
{
    [Fact]
    public void Export_lists_active_mods_with_versions_without_private_paths()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("Blue Moon", ("archive/pc/mod/bm.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        t.ImportZip("Off mod", ("archive/pc/mod/off.archive", "o"));
        var o = OverviewBuilder.Build(t.C);
        var text = ModListExport.Text(o, ExportScope.InGameNow, false, "0.4.0", DateTimeOffset.Now);
        Assert.Contains("Blue Moon", text);
        Assert.Contains("1.0", text);
        Assert.DoesNotContain("Off mod", text);
        Assert.DoesNotContain(t.Game, text);
        Assert.Contains("Off mod", ModListExport.Text(o, ExportScope.InGameNow, true, "0.4.0", DateTimeOffset.Now));
        Assert.StartsWith("order,name,version", ModListExport.Csv(o, ExportScope.InGameNow, false));
    }
}

public class StorageTests
{
    [Fact]
    public void Purge_deletes_kept_files_but_not_mods()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.Write("archive/pc/mod/manual.archive", "m");
        var s = t.C.Scan();
        t.C.RemoveUnmanaged(s.Detected.SelectMany(d => d.Files.Select(f => f.Path)).ToList(), "test");
        Assert.True(t.C.StorageInfo().RemovedBatches > 0);
        File.WriteAllText(Path.Combine(t.C.Library.Downloads, "x.zip"), "zip");
        var freed = t.C.PurgeStorage(true, true, true);
        Assert.True(freed > 0);
        var info = t.C.StorageInfo();
        Assert.Equal(0, info.RemovedBatches);
        Assert.Equal(0, info.Downloads);
        Assert.NotNull(t.C.Store.GetPackage(a.PackageId));
    }
}
