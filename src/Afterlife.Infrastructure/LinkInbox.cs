namespace Afterlife.Infrastructure;

/// <summary>
/// The fallback hand-over between two AFTERLIFE starts: when the named pipe cannot reach the running window (for
/// example because it runs as administrator), the second start writes its message as a file into this folder and the
/// running window reads it. Only an nxm:// link or "activate" is ever accepted, written or read.
/// </summary>
public sealed class LinkInbox(string folder)
{
    public const string Activate = "activate";
    private const string Extension = ".link";
    private readonly object _drainLock = new();
    private static long _sequence;

    public string Folder { get; } = folder;

    public static string DefaultFolder => Path.Combine(AppSettings.SettingsDirectory, "inbox");

    /// <summary>What may travel from one start to the other: a Mod Manager Download link, a collection link or "activate".</summary>
    public static bool IsAccepted(string? text)
    {
        var t = text?.Trim();
        if (string.IsNullOrEmpty(t)) return false;
        if (t.Equals(Activate, StringComparison.OrdinalIgnoreCase)) return true;
        if (!t.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase)) return false;
        return NxmLink.TryParse(t, out _) || CollectionLink.TryParse(t, out _);
    }

    /// <summary>Writes the message atomically (temporary file, then rename). Returns the file, or null when refused or not writable.</summary>
    public string? Drop(string message)
    {
        if (!IsAccepted(message)) return null;
        try
        {
            Directory.CreateDirectory(Folder);
            // the name sorts by time, so several links arriving together are handled in the order they were sent
            var name = $"{DateTime.UtcNow.Ticks:D19}-{Interlocked.Increment(ref _sequence):D8}-{Guid.NewGuid():N}";
            var tmp = Path.Combine(Folder, name + ".tmp");
            var path = Path.Combine(Folder, name + Extension);
            File.WriteAllText(tmp, message.Trim());
            File.Move(tmp, path);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// Takes every waiting message out of the folder, oldest first: each file is read and deleted. Files older than
    /// <paramref name="maxAge"/> (left behind when no window was there to read them) and anything that is not an
    /// accepted message are deleted without being returned. A file that cannot be read yet stays for the next call.
    /// </summary>
    public IReadOnlyList<string> Drain(TimeSpan maxAge)
    {
        lock (_drainLock)
        {
            var result = new List<string>();
            if (!Directory.Exists(Folder)) return result;
            string[] files;
            try { files = Directory.GetFiles(Folder); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return result; }
            var now = DateTime.UtcNow;
            foreach (var file in files.OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                try
                {
                    var age = now - File.GetLastWriteTimeUtc(file);
                    if (!file.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                    {
                        // a temporary file of a start that died before the rename
                        if (age > TimeSpan.FromMinutes(1)) File.Delete(file);
                        continue;
                    }
                    var text = age > maxAge || new FileInfo(file).Length > 4096 ? null : File.ReadAllText(file).Trim();
                    File.Delete(file);
                    if (IsAccepted(text)) result.Add(text!);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // still being written: next time
            }
            return result;
        }
    }
}
