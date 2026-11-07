using Afterlife.Infrastructure;

namespace Afterlife.Tests;

/// <summary>The overview reuses the last scan of the game folder while only profiles change.</summary>
public class ScanCacheTests
{
    [Fact]
    public void Cached_scan_is_reused_after_a_profile_change_and_dropped_after_a_library_change()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.Write("archive/pc/mod/Manual.archive", "manual");
        var first = t.C.Scan();
        Assert.Contains(first.Unmanaged, u => u.Path.EndsWith("Manual.archive", StringComparison.Ordinal));

        // a file dropped in by hand is not seen by the cached scan ...
        t.Write("archive/pc/mod/Later.archive", "later");
        t.C.SetEnabled(a.PackageId, true);
        var cached = t.C.CachedScan();
        Assert.DoesNotContain(cached.Unmanaged, u => u.Path.EndsWith("Later.archive", StringComparison.Ordinal));

        // ... but importing a package changes what the scan depends on, so it walks the folder again
        t.ImportZip("B", ("archive/pc/mod/b.archive", "b"));
        var fresh = t.C.CachedScan();
        Assert.Contains(fresh.Unmanaged, u => u.Path.EndsWith("Later.archive", StringComparison.Ordinal));

        // and a full scan always does
        t.Write("archive/pc/mod/Latest.archive", "latest");
        Assert.DoesNotContain(t.C.CachedScan().Unmanaged, u => u.Path.EndsWith("Latest.archive", StringComparison.Ordinal));
        Assert.Contains(t.C.Scan().Unmanaged, u => u.Path.EndsWith("Latest.archive", StringComparison.Ordinal));
    }

    [Fact]
    public void Cached_scan_hides_identical_copies_according_to_the_current_profile()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "same content"));
        t.Write("archive/pc/mod/a.archive", "same content"); // a copy left by a manual install
        var off = t.C.Scan();
        Assert.Contains(off.Detected, d => d.Files.Any(f => f.Path.EndsWith("a.archive", StringComparison.Ordinal)));

        t.C.SetEnabled(a.PackageId, true);
        var on = t.C.CachedScan();
        Assert.DoesNotContain(on.Detected, d => d.Files.Any(f => f.Path.EndsWith("a.archive", StringComparison.Ordinal)));
        Assert.Equal(off.Unmanaged.Count, on.Unmanaged.Count); // the file itself is still listed; apply adopts it
    }

    [Fact]
    public void Apply_invalidates_the_cached_scan()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.Scan();
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        t.Write("archive/pc/mod/AfterApply.archive", "x");
        Assert.Contains(t.C.CachedScan().Unmanaged, u => u.Path.EndsWith("AfterApply.archive", StringComparison.Ordinal));
    }
}
