using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Afterlife.Core;

/// <summary>Kind of a section line in a load list.</summary>
public enum LoadListSectionKind { Core, Collection, Mods, Category }

/// <summary>A layer (a mod's local edits) written as a comment under its mod; never read back.</summary>
public sealed record LoadListLayer(string Name, bool Enabled);

/// <summary>One mod line: <c>+ Name | version | nexus=mod/file | file=archive | md5=hash</c>.</summary>
public sealed record LoadListMod(
    int Line,
    bool Enabled,
    string Name,
    string? Version = null,
    string? NexusModId = null,
    string? NexusFileId = null,
    string? FileName = null,
    string? Md5 = null)
{
    /// <summary>Layers of this mod, written as comment lines by the writer; the reader leaves this empty.</summary>
    public IReadOnlyList<LoadListLayer> Layers { get; init; } = [];
}

/// <summary>A section of a load list with its mods in the order of the file. <see cref="Line"/> is 0 for the implicit MODS section of a list that starts with mod lines.</summary>
public sealed record LoadListSection(
    int Line,
    LoadListSectionKind Kind,
    string Name,
    IReadOnlyList<LoadListMod> Mods,
    string? Slug = null,
    int? Revision = null,
    bool Locked = false);

/// <summary>A load list as read from a file: sections in load order and the AFTERLIFE version named in its comments.</summary>
public sealed record LoadListDocument(IReadOnlyList<LoadListSection> Sections, string? MadeBy = null)
{
    public IEnumerable<LoadListMod> Mods => Sections.SelectMany(s => s.Mods);
}

/// <summary>What the comments at the top of an exported list say.</summary>
public sealed record LoadListHeader(string Profile, DateTimeOffset Exported, string AppVersion, bool Offline, bool DefaultOrder);

/// <summary>A list that was read, or the message that says why nothing can be loaded.</summary>
public sealed record LoadListReadResult(LoadListDocument? Document, string? Error);

/// <summary>
/// The load list: the load order of one profile as a text file (docs/previews/load-list-format.md). One line per
/// mod, sections for CORE MODS, collections, MODS and categories, <c>#</c> for comments. Top loads first and wins.
/// </summary>
public static class LoadList
{
    public const string Magic = "AFTERLIFE LOAD LIST";
    public const int FormatVersion = 1;

    public const string NotAListMessage = "This is not an AFTERLIFE load list. Nothing was loaded.";
    public const string ModLineProblem = "a mod line starts with + or -.";
    public const string DefaultOrderComment = "This profile uses the game's default order (by archive file name); the list below is by mod name.";

    public static string NewerMessage(int version) =>
        $"This list was made by a newer AFTERLIFE (format {version}). Update AFTERLIFE to load it.";

    public static string LineError(int line, string problem) => $"Line {line}: {problem} Nothing was loaded.";

    private static readonly Regex MadeByPattern = new(@"AFTERLIFE\s+(\d[^\s()]*)", RegexOptions.CultureInvariant);

    /// <summary>Suggested file name: <c>afterlife-order-&lt;profile&gt;-&lt;yyyy-MM-dd&gt;.txt</c>.</summary>
    public static string SuggestedFileName(string profile, DateTime date)
    {
        var safe = new StringBuilder();
        foreach (var ch in profile.Trim())
            safe.Append(char.IsControl(ch) || "<>:\"/\\|?*".Contains(ch) ? '_' : ch);
        var name = safe.Length == 0 ? "profile" : safe.ToString();
        return $"afterlife-order-{name}-{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.txt";
    }

    // ---------------- reading ----------------

    public static LoadListReadResult Read(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var first = 0;
        while (first < lines.Length && Clean(lines[first]).Length == 0) first++;
        if (first >= lines.Length) return Fail(NotAListMessage);
        var head = Clean(lines[first]);
        if (!head.StartsWith(Magic, StringComparison.OrdinalIgnoreCase)) return Fail(NotAListMessage);
        if (!int.TryParse(head[Magic.Length..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
            return Fail(NotAListMessage);
        if (version > FormatVersion) return Fail(NewerMessage(version));

        var built = new List<(LoadListSection Head, List<LoadListMod> Mods)>();
        string? madeBy = null;
        for (var n = first + 1; n < lines.Length; n++)
        {
            var lineNo = n + 1;
            var line = lines[n].Trim();
            if (line.Length == 0) continue;
            if (line[0] == '#')
            {
                if (madeBy is null && MadeByPattern.Match(line) is { Success: true } m) madeBy = m.Groups[1].Value;
                continue;
            }
            if (line[0] == '@')
            {
                var body = line[1..];
                var end = 0;
                while (end < body.Length && !char.IsWhiteSpace(body[end]) && body[end] != '|') end++;
                var word = body[..end].ToLowerInvariant();
                var parts = SplitFields(body[end..]);
                var name = parts.Count > 0 ? parts[0] : "";
                switch (word)
                {
                    case "core":
                        built.Add((new LoadListSection(lineNo, LoadListSectionKind.Core, "", []), new List<LoadListMod>()));
                        break;
                    case "mods":
                        built.Add((new LoadListSection(lineNo, LoadListSectionKind.Mods, "", []), new List<LoadListMod>()));
                        break;
                    case "category":
                        if (name.Length == 0) return Fail(LineError(lineNo, "@category needs a name."));
                        built.Add((new LoadListSection(lineNo, LoadListSectionKind.Category, name, []), new List<LoadListMod>()));
                        break;
                    case "collection":
                        if (name.Length == 0) return Fail(LineError(lineNo, "@collection needs a name."));
                        int? revision = int.TryParse(Field(parts, "revision"), NumberStyles.None, CultureInfo.InvariantCulture, out var r) ? r : null;
                        var locked = parts.Skip(1).Any(p => p.Equals("locked", StringComparison.OrdinalIgnoreCase));
                        built.Add((new LoadListSection(lineNo, LoadListSectionKind.Collection, name, [], Field(parts, "slug"), revision, locked), new List<LoadListMod>()));
                        break;
                    default:
                        return Fail(LineError(lineNo, $"unknown section @{body[..end]}; use @core, @collection, @mods or @category."));
                }
                continue;
            }
            if (line[0] is '+' or '-')
            {
                var parts = SplitFields(line[1..]);
                var name = parts.Count > 0 ? parts[0] : "";
                if (name.Length == 0) return Fail(LineError(lineNo, "a mod line needs a name after + or -."));
                var version2 = parts.Count > 1 && parts[1].Length > 0 && !parts[1].Contains('=') ? parts[1] : null;
                string? nexusMod = null, nexusFile = null;
                if (Field(parts, "nexus") is { } nexus)
                {
                    var ids = nexus.Split('/', StringSplitOptions.TrimEntries);
                    if (ids.Length >= 1 && IsNumber(ids[0]))
                    {
                        nexusMod = ids[0];
                        if (ids.Length >= 2 && IsNumber(ids[1])) nexusFile = ids[1];
                    }
                }
                if (built.Count == 0) built.Add((new LoadListSection(0, LoadListSectionKind.Mods, "", []), new List<LoadListMod>()));
                built[^1].Mods.Add(new LoadListMod(lineNo, line[0] == '+', name, version2, nexusMod, nexusFile, Field(parts, "file"), Field(parts, "md5")));
                continue;
            }
            return Fail(LineError(lineNo, ModLineProblem));
        }
        var sections = built.Select(b => b.Head with { Mods = b.Mods }).ToList();
        return new LoadListReadResult(new LoadListDocument(sections, madeBy), null);
    }

    private static LoadListReadResult Fail(string message) => new(null, message);

    private static string Clean(string line) => line.Trim().TrimStart('﻿').Trim();

    private static bool IsNumber(string s) => s.Length > 0 && s.All(char.IsAsciiDigit);

    /// <summary>Splits on <c>|</c>; <c>\|</c> is a <c>|</c> inside a part. Parts are trimmed.</summary>
    internal static List<string> SplitFields(string text)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\\' && i + 1 < text.Length && text[i + 1] == '|') { current.Append('|'); i++; }
            else if (ch == '|') { parts.Add(current.ToString().Trim()); current.Clear(); }
            else current.Append(ch);
        }
        parts.Add(current.ToString().Trim());
        return parts;
    }

    /// <summary>Value of a <c>key=value</c> part after the name; null when missing or empty. Unknown keys are ignored.</summary>
    private static string? Field(List<string> parts, string key)
    {
        foreach (var p in parts.Skip(1))
        {
            var eq = p.IndexOf('=');
            if (eq <= 0 || !p[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            var value = p[(eq + 1)..].Trim();
            return value.Length == 0 ? null : value;
        }
        return null;
    }

    // ---------------- writing ----------------

    public static string Write(LoadListHeader header, IReadOnlyList<LoadListSection> sections)
    {
        var sb = new StringBuilder();
        void Line(string text) => sb.Append(text).Append("\r\n");
        Line($"{Magic} {FormatVersion}");
        Line($"# Profile: {OneLine(header.Profile)} · exported {header.Exported.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} · AFTERLIFE {header.AppVersion} ({(header.Offline ? "Offline" : "full")})");
        Line("# Higher in the list loads first and wins conflicts.");
        Line("# + switched on, - switched off. Lines starting with # are notes.");
        Line("# You can move lines, change + and -, add @category lines. Load it back with IMPORT ORDER.");
        if (header.DefaultOrder) Line("# " + DefaultOrderComment);
        foreach (var s in sections)
        {
            Line("");
            switch (s.Kind)
            {
                case LoadListSectionKind.Core: Line("@core"); break;
                case LoadListSectionKind.Mods: Line("@mods"); break;
                case LoadListSectionKind.Category: Line("@category " + Escape(s.Name)); break;
                default:
                    var head = new StringBuilder("@collection ").Append(Escape(s.Name));
                    if (!string.IsNullOrWhiteSpace(s.Slug)) head.Append(" | slug=").Append(Escape(s.Slug));
                    if (s.Revision is { } rev) head.Append(" | revision=").Append(rev.ToString(CultureInfo.InvariantCulture));
                    if (s.Locked) head.Append(" | locked");
                    Line(head.ToString());
                    break;
            }
            foreach (var m in s.Mods)
            {
                Line(ModLine(m));
                foreach (var layer in m.Layers) Line($"#   └ {(layer.Enabled ? '+' : '-')} {OneLine(layer.Name)}");
            }
        }
        return sb.ToString();
    }

    private static string ModLine(LoadListMod m)
    {
        var sb = new StringBuilder();
        sb.Append(m.Enabled ? '+' : '-').Append(' ').Append(Escape(m.Name));
        if (!string.IsNullOrWhiteSpace(m.Version) && !m.Version.Contains('=')) sb.Append(" | ").Append(Escape(m.Version));
        if (!string.IsNullOrWhiteSpace(m.NexusModId))
        {
            sb.Append(" | nexus=").Append(m.NexusModId.Trim());
            if (!string.IsNullOrWhiteSpace(m.NexusFileId)) sb.Append('/').Append(m.NexusFileId.Trim());
        }
        // lines stay short: the archive name only without an exact Nexus file, the checksum only without a Nexus link
        if (string.IsNullOrWhiteSpace(m.NexusFileId) && !string.IsNullOrWhiteSpace(m.FileName)) sb.Append(" | file=").Append(Escape(m.FileName));
        if (string.IsNullOrWhiteSpace(m.NexusModId) && !string.IsNullOrWhiteSpace(m.Md5)) sb.Append(" | md5=").Append(Escape(m.Md5));
        return sb.ToString();
    }

    private static string OneLine(string text) => text.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string Escape(string text) => OneLine(text).Replace("|", "\\|");
}
