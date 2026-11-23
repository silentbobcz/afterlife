using System.Text;
using System.Text.RegularExpressions;

namespace Afterlife.Core;

public enum RedscriptOutcome { Unknown, Succeeded, Failed }

/// <summary>One error or warning of the redscript compiler.</summary>
/// <param name="File">Path of the script as the log writes it (absolute in a normal log); null when the entry names none.</param>
/// <param name="Message">What went wrong, without the code excerpt; empty when a cut log lost it.</param>
/// <param name="Text">The whole entry without its time stamp: level, location, code excerpt and message.</param>
public sealed record RedscriptError(string? File, int? Line, int? Column, string Message, string Text);

/// <param name="Finished">The log has its closing verdict (failed or saved); false for an empty or cut log.</param>
/// <param name="CausedBy">Names from the "caused by" list under the failure, in the order of the log.</param>
/// <param name="ScriptsFolder">The folder from "Compiling files in …", when the log names it.</param>
public sealed record RedscriptLogResult(
    RedscriptOutcome Outcome,
    bool Finished,
    IReadOnlyList<RedscriptError> Errors,
    IReadOnlyList<RedscriptError> Warnings,
    IReadOnlyList<string> CausedBy,
    string? ScriptsFolder)
{
    public static RedscriptLogResult Empty { get; } = new(RedscriptOutcome.Unknown, false, [], [], [], null);
}

/// <summary>
/// Reads the log the redscript compiler writes on every start of the game (<see cref="RelativePath"/>). Pure: the
/// caller reads the file. Tolerant of other wordings, both slashes, other encodings, empty and cut logs.
/// </summary>
public static class RedscriptLog
{
    public const string RelativePath = "r6/logs/redscript_rCURRENT.log";

    private const int MaxEntryLines = 12;

    private enum Level { Info, Warning, Error }

    private static readonly Dictionary<string, Level> Levels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INFO"] = Level.Info, ["DEBUG"] = Level.Info, ["TRACE"] = Level.Info,
        ["WARN"] = Level.Warning, ["WARNING"] = Level.Warning,
        ["ERROR"] = Level.Error, ["ERR"] = Level.Error, ["FATAL"] = Level.Error, ["CRITICAL"] = Level.Error,
    };

    // "[ERROR - Mon, 23 Nov 2026 10:11:12 +0100] text", or the same after a time stamp of its own in brackets
    private static readonly Regex Header = new(@"^\[\s*(?<level>[A-Za-z]+)\b[^\]]*\]\s?(?<rest>.*)$", RegexOptions.Compiled);
    private static readonly Regex StampedHeader = new(@"^\[[^\]]*\]\s*\[\s*(?<level>[A-Za-z]+)\b[^\]]*\]\s?(?<rest>.*)$", RegexOptions.Compiled);
    // "At C:\Games\Cyberpunk 2077\r6\scripts\Mod\Main.reds:42:15" (drive, rooted or relative path; the column is optional)
    private static readonly Regex Location = new(
        @"(?:\bAt\s+)?(?<path>[A-Za-z]:[\\/][^:*?""<>|\r\n]*?\.reds|[\\/][^:*?""<>|\r\n]*?\.reds|[^\s:*?""<>|]+\.reds)\s*:\s*(?<line>\d+)(?:\s*:\s*(?<col>\d+))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FailedMark = new(@"(?i)compilation\s+(?:has\s+)?failed|failed\s+to\s+compile", RegexOptions.Compiled);
    private static readonly Regex SuccessContext = new(@"(?i)sav|compil|writ|wrote|output|redscripts", RegexOptions.Compiled);
    private static readonly Regex NotSuccess = new(@"(?i)\b(?:not|un)\s*successful|fail", RegexOptions.Compiled);
    private static readonly Regex CausedByMark = new(@"(?i)\bcaused\s+by\b", RegexOptions.Compiled);
    private static readonly Regex Bullet = new(@"^\s*(?:[-*•]|\d+[.)])\s+(?<name>.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex Caret = new(@"^\s*[\^~][\s\^~-]*$", RegexOptions.Compiled);
    private static readonly Regex ScriptsMark = new(@"(?i)\bcompiling\s+(?:files\s+)?in\s+(?<dir>.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex Slashes = new(@"/{2,}", RegexOptions.Compiled);

    private sealed class Entry(Level level, string levelText, string rest)
    {
        public Level Level { get; } = level;
        public string LevelText { get; } = levelText;
        public string Rest { get; } = rest;
        public List<string> Body { get; } = [];
    }

    /// <summary>
    /// Text of the log file: UTF-8 with or without BOM, UTF-16 with or without BOM, a UTF-8 log cut inside a
    /// character, anything else read as Latin-1 (paths and mod names stay readable).
    /// </summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length == 0) return "";
        string text;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        else if (LooksLikeUtf16(bytes, out var bigEndian)) text = (bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode).GetString(bytes);
        else text = StrictUtf8(bytes) ?? Encoding.Latin1.GetString(bytes);
        return text.Replace("\0", "");
    }

    private static bool LooksLikeUtf16(byte[] bytes, out bool bigEndian)
    {
        int n = Math.Min(bytes.Length, 1024) & ~1, even = 0, odd = 0;
        for (var i = 0; i < n; i += 2)
        {
            if (bytes[i] == 0) even++;
            if (bytes[i + 1] == 0) odd++;
        }
        var half = n / 2;
        bigEndian = even > odd;
        return half >= 2 && Math.Max(even, odd) * 10 >= half * 4 && Math.Min(even, odd) * 10 < half;
    }

    private static string? StrictUtf8(byte[] bytes)
    {
        var strict = new UTF8Encoding(false, true);
        // a log cut while the game wrote it may end inside a character: drop up to three bytes of it
        for (var cut = 0; cut <= 3 && cut < bytes.Length; cut++)
        {
            try { return strict.GetString(bytes, 0, bytes.Length - cut); }
            catch (DecoderFallbackException) { }
        }
        return null;
    }

    public static RedscriptLogResult Parse(byte[] bytes) => Parse(Decode(bytes));

    public static RedscriptLogResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return RedscriptLogResult.Empty;
        var lines = text.TrimStart('\uFEFF').Replace("\0", "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        var entries = new List<Entry>();
        Entry? current = null;
        bool failed = false, succeeded = false;
        string? scripts = null;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (FailedMark.IsMatch(line)) failed = true;
            else if (line.Contains("success", StringComparison.OrdinalIgnoreCase) && SuccessContext.IsMatch(line) && !NotSuccess.IsMatch(line)) succeeded = true;

            if (TryHeader(line, out var level, out var levelText, out var rest))
            {
                current = new Entry(level, levelText, rest);
                entries.Add(current);
                if (scripts is null && ScriptsMark.Match(current.Rest) is { Success: true } s)
                    scripts = s.Groups["dir"].Value.TrimEnd('.', ' ', ':');
            }
            else current?.Body.Add(line);
        }

        var errors = new List<RedscriptError>();
        var warnings = new List<RedscriptError>();
        foreach (var e in entries)
        {
            if (e.Level == Level.Info) continue;
            var error = ParseEntry(e, out var located);
            // the closing summary ("compilation has failed", the caused-by list) is the verdict, not an error of its own
            if (!located && (FailedMark.IsMatch(e.Rest) || e.Body.Any(CausedByMark.IsMatch))) continue;
            (e.Level == Level.Error ? errors : warnings).Add(error);
        }

        var outcome = failed ? RedscriptOutcome.Failed
            : succeeded ? RedscriptOutcome.Succeeded
            : errors.Count > 0 ? RedscriptOutcome.Failed
            : RedscriptOutcome.Unknown;
        return new RedscriptLogResult(outcome, failed || succeeded, errors, warnings, CausedBy(lines), scripts);
    }

    private static bool TryHeader(string line, out Level level, out string levelText, out string rest)
    {
        foreach (var regex in new[] { Header, StampedHeader })
        {
            var h = regex.Match(line);
            if (h.Success && Levels.TryGetValue(h.Groups["level"].Value, out level))
            {
                levelText = h.Groups["level"].Value.ToUpperInvariant();
                rest = h.Groups["rest"].Value.Trim();
                return true;
            }
        }
        level = Level.Info;
        levelText = rest = "";
        return false;
    }

    private static RedscriptError ParseEntry(Entry e, out bool located)
    {
        Match? loc = null;
        var locLine = -1; // -1: in the header line
        var m = Location.Match(e.Rest);
        if (m.Success) loc = m;
        else
            for (var i = 0; i < e.Body.Count; i++)
            {
                m = Location.Match(e.Body[i]);
                if (m.Success) { loc = m; locLine = i; break; }
            }
        located = loc is not null;

        var parts = new List<string>();
        var head = loc is not null && locLine < 0 ? e.Rest.Remove(loc.Index, loc.Length) : e.Rest;
        head = head.Trim().Trim(':', '-').Trim();
        if (head.Length > 0) parts.Add(head);
        for (var i = 0; i < e.Body.Count; i++)
        {
            var b = e.Body[i];
            // indented lines are the code excerpt, carets point into it; the message starts at the margin
            if (i == locLine || string.IsNullOrWhiteSpace(b) || Caret.IsMatch(b) || char.IsWhiteSpace(b[0])) continue;
            parts.Add(b.Trim());
        }

        var body = e.Body.Take(MaxEntryLines).ToList();
        while (body.Count > 0 && string.IsNullOrWhiteSpace(body[^1])) body.RemoveAt(body.Count - 1);
        var text = string.Join("\n", new[] { $"[{e.LevelText}] {e.Rest}" }.Concat(body));

        if (loc is null) return new RedscriptError(null, null, null, string.Join(" ", parts), text);
        int? line = int.TryParse(loc.Groups["line"].Value, out var l) ? l : null;
        int? col = loc.Groups["col"].Success && int.TryParse(loc.Groups["col"].Value, out var c) ? c : null;
        return new RedscriptError(loc.Groups["path"].Value.Trim(), line, col, string.Join(" ", parts), text);
    }

    private static List<string> CausedBy(string[] lines)
    {
        var names = new List<string>();
        void Add(string name)
        {
            name = name.Trim().Trim('"', '\'', '`', ',', ';', '.').Trim();
            if (name.Length > 0 && !names.Contains(name, StringComparer.OrdinalIgnoreCase)) names.Add(name);
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var mark = CausedByMark.Match(lines[i]);
            if (!mark.Success) continue;
            var found = false;
            for (var j = i + 1; j < lines.Length; j++)
            {
                var l = lines[j];
                if (string.IsNullOrWhiteSpace(l)) { if (found) break; continue; }
                if (TryHeader(l, out _, out _, out _)) break;
                var b = Bullet.Match(l);
                if (!b.Success) break;
                Add(b.Groups["name"].Value);
                found = true;
            }
            if (found) continue;
            // the same line: "caused by mods: A, B"
            var after = lines[i][(mark.Index + mark.Length)..];
            var colon = after.IndexOf(':');
            if (colon < 0) continue;
            foreach (var name in after[(colon + 1)..].Split(','))
                if (!name.Contains("below", StringComparison.OrdinalIgnoreCase)) Add(name);
        }
        return names;
    }

    /// <summary>
    /// Game-relative path ('/' separators) of a script named in the log: under <paramref name="gameRoot"/> when it
    /// starts with it, otherwise from its <c>r6/scripts</c>, <c>red4ext/plugins</c> or <c>mods</c> part; a relative
    /// path is taken as relative to <c>r6/scripts</c>. Null when it cannot be placed in the game folder.
    /// </summary>
    public static string? ToGamePath(string file, string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        var f = Slashes.Replace(file.Trim().Replace('\\', '/'), "/");
        string? rel = null;
        if (!string.IsNullOrWhiteSpace(gameRoot))
        {
            var root = Slashes.Replace(gameRoot.Trim().Replace('\\', '/'), "/").TrimEnd('/') + "/";
            if (f.StartsWith(root, StringComparison.OrdinalIgnoreCase)) rel = f[root.Length..];
        }
        if (rel is null)
        {
            var rooted = f.StartsWith('/') || (f.Length >= 2 && f[1] == ':');
            if (!rooted)
                rel = f.StartsWith("./", StringComparison.Ordinal) ? "r6/scripts/" + f[2..]
                    : IsUnderAnyRoot(f) ? f
                    : "r6/scripts/" + f;
            else
                foreach (var marker in new[] { "/r6/scripts/", "/red4ext/plugins/", "/mods/" })
                {
                    var i = f.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (i >= 0) { rel = f[(i + 1)..]; break; }
                }
        }
        return rel is not null && GamePath.TryCreate(rel, out var p, out _) ? p.Value : null;
    }

    private static bool IsUnderAnyRoot(string relative) =>
        relative.StartsWith("r6/scripts/", StringComparison.OrdinalIgnoreCase)
        || relative.StartsWith("red4ext/plugins/", StringComparison.OrdinalIgnoreCase)
        || relative.StartsWith("mods/", StringComparison.OrdinalIgnoreCase);

    /// <summary>The module folder of a script directly under <c>r6/scripts</c> ("r6/scripts/Mod/x/a.reds" → "Mod"); null for a file directly in it or elsewhere.</summary>
    public static string? ScriptFolder(string gamePath)
    {
        const string root = "r6/scripts/";
        var p = gamePath.Replace('\\', '/');
        if (!p.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
        var rest = p[root.Length..];
        var slash = rest.IndexOf('/');
        return slash > 0 ? rest[..slash] : null;
    }
}
