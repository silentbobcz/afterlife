using System.Text;

namespace Afterlife.Core;

/// <summary>
/// A validated, normalized path relative to the game root (or to an archive root).
/// Separator is always '/'. Comparison is case-insensitive (Windows semantics) via <see cref="Key"/>.
/// </summary>
public readonly struct GamePath : IEquatable<GamePath>, IComparable<GamePath>
{
    public const int MaxLength = 200;

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private const string InvalidChars = "<>:\"|?*";

    public string Value { get; }
    public string Key { get; }

    private GamePath(string value)
    {
        Value = value;
        Key = value.ToUpperInvariant();
    }

    public static bool TryCreate(string? raw, out GamePath path, out string? error)
    {
        path = default;
        error = null;
        if (string.IsNullOrWhiteSpace(raw)) { error = "empty path"; return false; }

        var s = raw.Replace('\\', '/');
        if (!s.IsNormalized(NormalizationForm.FormC)) s = s.Normalize(NormalizationForm.FormC);

        if (s.StartsWith('/')) { error = "absolute path"; return false; }
        if (s.Length >= 2 && s[1] == ':') { error = "drive-qualified path"; return false; }
        if (s.Length > MaxLength) { error = $"path longer than {MaxLength} characters"; return false; }

        foreach (var seg in s.Split('/'))
        {
            if (seg.Length == 0) { error = "empty path segment"; return false; }
            if (seg == "." || seg == "..") { error = "path traversal segment"; return false; }
            foreach (var ch in seg)
            {
                if (ch < 0x20) { error = "control character in path"; return false; }
                if (InvalidChars.IndexOf(ch) >= 0)
                {
                    error = ch == ':' ? "alternate data stream or drive marker" : $"invalid character '{ch}'";
                    return false;
                }
            }
            if (seg.EndsWith('.') || seg.EndsWith(' ')) { error = "segment ends with dot or space"; return false; }
            var stem = seg.Split('.')[0].TrimEnd();
            if (ReservedNames.Contains(stem)) { error = $"reserved Windows name '{stem}'"; return false; }
        }

        path = new GamePath(s);
        return true;
    }

    public static GamePath Parse(string raw) =>
        TryCreate(raw, out var p, out var e) ? p : throw new ArgumentException($"Invalid game path '{raw}': {e}");

    public string FileName => Value[(Value.LastIndexOf('/') + 1)..];

    public string? ParentValue
    {
        get
        {
            var i = Value.LastIndexOf('/');
            return i < 0 ? null : Value[..i];
        }
    }

    public string Extension
    {
        get
        {
            var name = FileName;
            var i = name.LastIndexOf('.');
            return i < 0 ? "" : name[i..].ToLowerInvariant();
        }
    }

    public bool IsUnder(string directoryPrefix)
    {
        var p = directoryPrefix.Replace('\\', '/').TrimEnd('/').ToUpperInvariant() + "/";
        return Key.StartsWith(p, StringComparison.Ordinal);
    }

    public string FirstSegmentKey
    {
        get
        {
            var i = Key.IndexOf('/');
            return i < 0 ? Key : Key[..i];
        }
    }

    public GamePath Combine(string relative) => Parse(Value + "/" + relative);

    public string ToOsPath(string root) =>
        System.IO.Path.Combine(root, Value.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public bool Equals(GamePath other) => string.Equals(Key, other.Key, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is GamePath g && Equals(g);
    public override int GetHashCode() => Key?.GetHashCode(StringComparison.Ordinal) ?? 0;
    public int CompareTo(GamePath other) => string.CompareOrdinal(Key, other.Key);
    public static bool operator ==(GamePath a, GamePath b) => a.Equals(b);
    public static bool operator !=(GamePath a, GamePath b) => !a.Equals(b);
    public override string ToString() => Value;
}
