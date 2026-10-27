using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

/// <summary>P98: new version in a collection; P99: duplicates; P101: profile rename.</summary>
public class P98P101Tests
{
    [Fact]
    public void The_same_files_imported_again_give_the_existing_package_P99()
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
    public void Duplicates_are_merged_into_one_package_P99()
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

    [Fact]
    public void A_new_version_takes_the_place_of_the_old_one_in_collections_P98()
    {
        using var t = new TestEnv();
        var v1 = t.C.Import(new ImportRequest(t.Zip("Mod-1", ("archive/pc/mod/m.archive", "v1")), "Mod", "1.0")).Package!;
        var v2 = t.C.Import(new ImportRequest(t.Zip("Mod-2", ("archive/pc/mod/m.archive", "v2")), "Mod", "2.0", ExistingModId: v1.ModId)).Package!;
        CollectionStore.Save(t.C.Store, new CollectionRecord(CollectionTests.Api(), null, new Dictionary<string, string> { ["file:11"] = v1.PackageId }, [], [], true, DateTimeOffset.UtcNow));
        t.C.ReplaceInCollections(v1.PackageId, v2.PackageId);
        Assert.Equal(v2.PackageId, t.C.Collections().Single().PackageOf["file:11"]);
    }

    [Fact]
    public void Profiles_can_be_renamed_but_not_to_an_existing_name_P101()
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
