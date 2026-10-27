using System.Text;

namespace Afterlife.Core;

public enum DetectedKind { Archive, Redscript, Tweak, Input, CetMod, Red4extPlugin, Redmod, Framework, Other }

/// <summary>A best-effort group of files that were installed outside AFTERLIFE and probably belong together.</summary>
public sealed record DetectedMod(
    string Id,
    string Name,
    DetectedKind Kind,
    IReadOnlyList<(string Path, long Size)> Files,
    bool Removable,
    string? Note)
{
    public long TotalSize => Files.Sum(f => f.Size);
    public IReadOnlyList<DetectedKind> Kinds { get; init; } = [];
    public bool IsFramework => Kind == DetectedKind.Framework;
    /// <summary>Capability this group provides when it is a core mod (P15).</summary>
    public string? Capability => IsFramework && UnmanagedGrouping.FrameworkCapabilities.TryGetValue(Name, out var c) ? c : null;
    /// <summary>P32: library mods that cannot be installed while these files are in the game.</summary>
    public IReadOnlyList<string> Blocks { get; init; } = [];
}

/// <summary>
/// Groups unmanaged files into likely mods (P6). Heuristics only; nothing is inferred from file contents:
/// <c>X.archive</c> + <c>X.archive.xl</c> + <c>X.xl</c>; first folder below r6/scripts, r6/tweaks, CET mods,
/// red4ext/plugins and REDmod mods; groups with the same normalized name are merged.
/// Framework files (CET, RED4ext and its framework plugins) are grouped separately; since P15 they can be removed too.
/// </summary>
public static class UnmanagedGrouping
{
    /// <summary>
    /// P32: marks groups whose files block library mods, and gathers loose single files that only exist because
    /// a library mod wants the same path into one group per blocked mod ("In the way of X").
    /// </summary>
    /// <param name="same">P45: files with exactly the content a library mod ships (path → mod names).</param>
    /// <param name="hidden">P45: identical files of mods enabled in the selected profile; APPLY takes them over, so they are not listed.</param>
    public static IReadOnlyList<DetectedMod> WithBlockers(IReadOnlyList<DetectedMod> groups, IReadOnlyDictionary<string, IReadOnlyList<string>> inTheWay,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? same = null, IReadOnlySet<string>? hidden = null)
    {
        same ??= new Dictionary<string, IReadOnlyList<string>>();
        if (inTheWay.Count == 0 && same.Count == 0) return groups;
        var result = new List<DetectedMod>();
        var loose = new Dictionary<string, List<(string Path, long Size)>>(StringComparer.OrdinalIgnoreCase);
        var looseSame = new Dictionary<string, List<(string Path, long Size)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var g0 in groups)
        {
            var g = g0;
            if (hidden is { Count: > 0 })
            {
                var files = g.Files.Where(f => !hidden.Contains(f.Path)).ToList();
                if (files.Count == 0) continue;
                if (files.Count != g.Files.Count) g = g with { Files = files };
            }
            var sameAs = g.Files.Where(f => same.ContainsKey(f.Path)).SelectMany(f => same[f.Path]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (sameAs.Count > 0 && g.Files.All(f => same.ContainsKey(f.Path)))
            {
                if (g.Kind == DetectedKind.Other)
                {
                    foreach (var f in g.Files)
                    {
                        var owner = same[f.Path][0];
                        if (!looseSame.TryGetValue(owner, out var list)) looseSame[owner] = list = new();
                        list.Add(f);
                    }
                    continue;
                }
                result.Add(g with { Note = (g.Note is null ? "" : g.Note + " ") + $"Same files as {string.Join(", ", sameAs)} in your library; AFTERLIFE takes them over when that mod is applied." });
                continue;
            }
            var blocked = g.Files.Where(f => inTheWay.ContainsKey(f.Path)).SelectMany(f => inTheWay[f.Path]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (blocked.Count == 0) { result.Add(g); continue; }
            if (g.Kind == DetectedKind.Other)
            {
                foreach (var f in g.Files)
                {
                    var owner = inTheWay.TryGetValue(f.Path, out var o) ? o[0] : blocked[0];
                    if (!loose.TryGetValue(owner, out var list)) loose[owner] = list = new();
                    list.Add(f);
                }
                continue;
            }
            result.Add(g with
            {
                Blocks = blocked,
                Note = (g.Note is null ? "" : g.Note + " ") + $"Blocks {string.Join(", ", blocked)} from the library.",
            });
        }
        foreach (var (owner, files) in looseSame)
            result.Add(new DetectedMod("same:" + Normalize(owner), $"Same as {owner} in the library", DetectedKind.Other,
                files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList(), true,
                $"Left from an earlier install, identical to the files {owner} ships. Nothing to do: AFTERLIFE takes them over when {owner} is applied."));
        foreach (var (owner, files) in loose)
            result.Add(new DetectedMod("way:" + Normalize(owner), $"In the way of {owner}", DetectedKind.Other,
                files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList(), true,
                $"Different version of files {owner} ships, left from an earlier install. Remove them here, or use REPLACE on {owner}.")
            { Blocks = [owner], Kinds = [DetectedKind.Other] });
        return result.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static readonly Dictionary<string, string> FrameworkPluginFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ArchiveXL"] = "ArchiveXL",
        ["TweakXL"] = "TweakXL",
        ["Codeware"] = "Codeware",
        ["input_loader"] = "Input Loader",
        ["mod_settings"] = "Mod Settings",
        ["RedFileSystem"] = "RedFileSystem",
    };

    /// <summary>Group name → capability, so the window can warn about mods that need a framework before it is removed (P15).</summary>
    public static readonly IReadOnlyDictionary<string, string> FrameworkCapabilities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Cyber Engine Tweaks"] = Capabilities.Cet,
        ["RED4ext"] = Capabilities.Red4ext,
        ["redscript"] = Capabilities.Redscript,
        ["ArchiveXL"] = Capabilities.ArchiveXl,
        ["TweakXL"] = Capabilities.TweakXl,
        ["Codeware"] = Capabilities.Codeware,
        ["Input Loader"] = Capabilities.InputLoader,
        ["Mod Settings"] = Capabilities.ModSettings,
        ["Native Settings UI"] = Capabilities.NativeSettings,
        ["RedFileSystem"] = Capabilities.RedFileSystem,
    };

    public static IReadOnlyList<DetectedMod> Group(IEnumerable<(string Path, long Size)> files)
    {
        var groups = new Dictionary<string, Builder>(StringComparer.OrdinalIgnoreCase);

        void Add(string key, string name, DetectedKind kind, (string, long) file, bool framework = false)
        {
            if (!groups.TryGetValue(key, out var g)) groups[key] = g = new Builder(key, name, framework);
            g.Files.Add(file);
            g.Kinds.Add(kind);
        }

        foreach (var f in files)
        {
            var parts = f.Path.Split('/');
            var lower = f.Path.ToLowerInvariant();
            if (lower.StartsWith("archive/pc/mod/") && parts.Length == 4)
            {
                if (parts[3].Equals("modlist.txt", StringComparison.OrdinalIgnoreCase)) { Add("other:modlist", "modlist.txt (archive order)", DetectedKind.Other, f); continue; }
                var stem = ArchiveStem(parts[3]);
                Add("mod:" + Normalize(stem), stem, DetectedKind.Archive, f);
            }
            else if (lower.StartsWith("r6/scripts/") || lower.StartsWith("r6/tweaks/"))
            {
                var kind = lower.StartsWith("r6/scripts/") ? DetectedKind.Redscript : DetectedKind.Tweak;
                var name = parts.Length > 3 ? parts[2] : Path.GetFileNameWithoutExtension(parts[^1]);
                if (FrameworkPluginFolders.TryGetValue(name, out var fw)) { Add("fw:" + Normalize(fw), fw, kind, f, framework: true); continue; }
                Add("mod:" + Normalize(name), name, kind, f);
            }
            else if (lower.StartsWith("r6/input/"))
                Add("mod:" + Normalize(Path.GetFileNameWithoutExtension(parts[^1])), Path.GetFileNameWithoutExtension(parts[^1]), DetectedKind.Input, f);
            else if (lower.StartsWith("bin/x64/plugins/cyber_engine_tweaks/mods/nativesettings/"))
                Add("fw:nativesettings", "Native Settings UI", DetectedKind.Framework, f, framework: true);
            else if (lower.StartsWith("bin/x64/plugins/cyber_engine_tweaks/mods/") && parts.Length > 6)
                Add("mod:" + Normalize(parts[5]), parts[5], DetectedKind.CetMod, f);
            else if (lower.StartsWith("bin/x64/plugins/cyber_engine_tweaks") || lower == "bin/x64/plugins/cyber_engine_tweaks.asi")
                Add("fw:cet", "Cyber Engine Tweaks", DetectedKind.Framework, f, framework: true);
            else if (lower.StartsWith("red4ext/plugins/") && parts.Length > 3)
            {
                if (FrameworkPluginFolders.TryGetValue(parts[2], out var fw)) Add("fw:" + Normalize(fw), fw, DetectedKind.Framework, f, framework: true);
                else Add("mod:" + Normalize(parts[2]), parts[2], DetectedKind.Red4extPlugin, f);
            }
            else if (lower.StartsWith("red4ext/"))
                Add("fw:red4ext", "RED4ext", DetectedKind.Framework, f, framework: true);
            else if (lower is "bin/x64/version.dll" or "bin/x64/global.ini")
                Add("fw:cet", "Cyber Engine Tweaks", DetectedKind.Framework, f, framework: true);
            else if (lower == "bin/x64/winmm.dll")
                Add("fw:red4ext", "RED4ext", DetectedKind.Framework, f, framework: true);
            else if (lower.StartsWith("engine/tools/") || lower.StartsWith("engine/config/base/scripts.ini") || lower == "r6/config/cybercmd/scc.toml")
                Add("fw:redscript", "redscript", DetectedKind.Framework, f, framework: true);
            else if (lower.StartsWith("mods/") && parts.Length > 2)
                Add("redmod:" + Normalize(parts[1]), parts[1], DetectedKind.Redmod, f);
            else
                Add("other:" + lower, parts[^1], DetectedKind.Other, f);
        }

        return groups.Values
            .Select(g => g.Build())
            .OrderBy(g => g.Kind == DetectedKind.Framework)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>"X.archive", "X.archive.xl", "X.xl" → "X".</summary>
    public static string ArchiveStem(string fileName)
    {
        var s = fileName;
        foreach (var ext in new[] { ".xl", ".archive" })
            if (s.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) s = s[..^ext.Length];
        if (s.EndsWith(".archive", StringComparison.OrdinalIgnoreCase)) s = s[..^".archive".Length];
        return s.Length == 0 ? fileName : s;
    }

    /// <summary>Lowercase letters and digits only, so "Night City Interactions" and "NightCityInteractions" match.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        return sb.Length == 0 ? s.ToLowerInvariant() : sb.ToString();
    }

    private sealed class Builder(string key, string name, bool framework)
    {
        public List<(string Path, long Size)> Files { get; } = new();
        public HashSet<DetectedKind> Kinds { get; } = new();

        public DetectedMod Build()
        {
            var kind = framework ? DetectedKind.Framework
                : Kinds.Count == 1 ? Kinds.First()
                : Kinds.Contains(DetectedKind.Redmod) ? DetectedKind.Redmod
                : Kinds.Contains(DetectedKind.Archive) ? DetectedKind.Archive
                : Kinds.First();
            var note = framework
                ? "Core mod installed manually. Mods that need it stop working when it is removed."
                : kind == DetectedKind.Other ? "Single file that does not match a known mod layout." : null;
            return new DetectedMod(key, name, kind, Files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList(), true, note)
            {
                Kinds = Kinds.OrderBy(k => k).ToList(),
            };
        }
    }
}
