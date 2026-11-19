namespace Afterlife.Infrastructure;

/// <summary>
/// Hashes of game files remembered by path, size and time of the last write, so a file a mod keeps rewriting
/// (settings, a database) is not read in full on every refresh of the window. Used for previews only: applying
/// starts with an empty cache, and the deployment checks every file it touches against the disk again.
/// </summary>
public sealed class HashCache(IFileOps fs)
{
    /// <summary>
    /// A file written a moment ago can change once more without a new timestamp (the clock of the file system is
    /// coarse), so its hash is remembered only once the last write is this old.
    /// </summary>
    private static readonly TimeSpan Settled = TimeSpan.FromSeconds(2);

    private readonly Dictionary<string, (long Size, long MTimeTicks, string Hash)> _known = new(StringComparer.OrdinalIgnoreCase);

    public string Hash(string osPath)
    {
        var before = fs.GetMeta(osPath);
        if (before is not null && _known.TryGetValue(osPath, out var k) && k.Size == before.Size && k.MTimeTicks == before.MTimeTicks)
            return k.Hash;
        var hash = fs.Hash(osPath);
        var after = fs.GetMeta(osPath);
        var settled = before is not null && after is not null && after.Size == before.Size && after.MTimeTicks == before.MTimeTicks
            && DateTime.UtcNow.Ticks - before.MTimeTicks > Settled.Ticks;
        if (settled) _known[osPath] = (before!.Size, before.MTimeTicks, hash);
        else _known.Remove(osPath);
        return hash;
    }

    public void Clear() => _known.Clear();
}
