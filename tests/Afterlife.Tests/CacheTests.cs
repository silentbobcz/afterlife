using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

/// <summary>Packages and the ledger are kept as read; every way they can change must drop them.</summary>
public class StoreCacheTests
{
    [Fact]
    public void Packages_and_ledger_follow_every_write()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        Assert.Single(t.C.Store.GetPackages());
        Assert.Same(t.C.Store.GetPackages(), t.C.Store.GetPackages()); // read once, handed out again

        var b = t.ImportZip("B", ("archive/pc/mod/b.archive", "b"));
        Assert.Equal(2, t.C.Store.GetPackages().Count);
        t.C.RenamePackage(a.PackageId, "Renamed");
        Assert.Equal("Renamed", t.C.Store.GetPackage(a.PackageId)!.ModName);
        Assert.Contains(t.C.Store.GetPackages(), p => p.ModName == "Renamed");

        Assert.Empty(t.C.Store.GetLedger());
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        Assert.Single(t.C.Store.GetLedger());
        Assert.Same(t.C.Store.GetLedger(), t.C.Store.GetLedger());
        t.C.SetEnabled(a.PackageId, false);
        t.ApplyOk();
        Assert.Empty(t.C.Store.GetLedger());

        t.C.DeleteEverywhere([b.PackageId]);
        Assert.Null(t.C.Store.GetPackage(b.PackageId));
        Assert.Single(t.C.Store.GetPackages());
    }

    [Fact]
    public void A_rolled_back_transaction_leaves_nothing_behind_in_memory()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        Assert.Throws<InvalidOperationException>(() => t.C.Store.InTransaction(() =>
        {
            t.C.Store.UpdatePackage(a with { ModName = "Half done" });
            Assert.Equal("Half done", t.C.Store.GetPackage(a.PackageId)!.ModName); // its own writes are visible inside
            throw new InvalidOperationException("stop");
        }));
        Assert.Equal("A", t.C.Store.GetPackage(a.PackageId)!.ModName);
    }

    [Fact]
    public void A_write_from_a_second_window_is_seen()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        Assert.Equal("A", t.C.Store.GetPackage(a.PackageId)!.ModName);
        using (var other = AfterlifeStore.Open(t.C.Library.Database))
            other.UpdatePackage(a with { ModName = "From the other window" });
        Assert.Equal("From the other window", t.C.Store.GetPackage(a.PackageId)!.ModName);
    }
}

/// <summary>Hashes and file observations remembered between previews.</summary>
public class PreviewCacheTests
{
    private const string Rel = "archive/pc/mod/big.archive";

    [Fact]
    public void A_settled_file_is_read_once_until_its_size_or_time_changes()
    {
        using var t = new TestEnv();
        t.Write(Rel, "first");
        var path = t.GameFile(Rel);
        var longAgo = DateTime.UtcNow.AddMinutes(-5);
        File.SetLastWriteTimeUtc(path, longAgo);
        var cache = new HashCache(new PhysicalFileOps());
        Assert.Equal(ContentHash.Of("first"), cache.Hash(path));

        // same size and time: answered from memory, the file is not read (other content proves it)
        File.WriteAllText(path, "FIRST");
        File.SetLastWriteTimeUtc(path, longAgo);
        Assert.Equal(ContentHash.Of("first"), cache.Hash(path));

        // another time of the last write is another file
        File.SetLastWriteTimeUtc(path, longAgo.AddSeconds(1));
        Assert.Equal(ContentHash.Of("FIRST"), cache.Hash(path));

        // and an emptied cache reads everything again
        File.WriteAllText(path, "third");
        File.SetLastWriteTimeUtc(path, longAgo.AddSeconds(1));
        cache.Clear();
        Assert.Equal(ContentHash.Of("third"), cache.Hash(path));
    }

    [Fact]
    public void A_file_written_a_moment_ago_is_never_remembered()
    {
        using var t = new TestEnv();
        t.Write(Rel, "one");
        var path = t.GameFile(Rel);
        var written = File.GetLastWriteTimeUtc(path);
        var cache = new HashCache(new PhysicalFileOps());
        Assert.Equal(ContentHash.Of("one"), cache.Hash(path));
        File.WriteAllText(path, "two");
        File.SetLastWriteTimeUtc(path, written); // even with the very same size and time
        Assert.Equal(ContentHash.Of("two"), cache.Hash(path));
    }

    [Fact]
    public void A_preview_without_a_walk_trusts_what_the_last_one_saw_and_a_walk_asks_the_disk()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        Assert.True(t.C.Preview().Plan.IsNoop);

        // removed by hand: like a file dropped in by hand, it is noticed by the next walk of the game folder
        File.Delete(t.GameFile("archive/pc/mod/a.archive"));
        Assert.True(t.C.Preview(rescan: false).Plan.IsNoop);
        Assert.Contains(t.C.Preview().Plan.Steps, s => s.Kind == StepKind.Add && s.Path == "archive/pc/mod/a.archive");
    }

    [Fact]
    public void Applying_never_plans_from_memory()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        Assert.True(t.C.Preview().Plan.IsNoop);

        // the file disappears behind AFTERLIFE's back; apply walks the folder itself and puts it back
        File.Delete(t.GameFile("archive/pc/mod/a.archive"));
        var r = t.C.Apply();
        Assert.True(r.Succeeded, r.Message);
        Assert.Equal("a", t.Read("archive/pc/mod/a.archive"));
    }
}
