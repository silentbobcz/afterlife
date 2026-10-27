using System.Xml.Linq;

namespace Afterlife.Core;

/// <summary>One file or folder a FOMOD installer copies. Paths are relative to the folder that contains "fomod/".</summary>
public sealed record FomodFile(string Source, string Destination, bool IsFolder, int Priority);

public sealed record FomodFlag(string Name, string Value);

/// <summary>Flag condition (And/Or); file and game version conditions are treated as met.</summary>
public sealed record FomodCondition(bool Or, IReadOnlyList<FomodFlag> Flags)
{
    public bool IsMet(IReadOnlyDictionary<string, string> flags) =>
        Flags.Count == 0 || (Or
            ? Flags.Any(f => flags.TryGetValue(f.Name, out var v) && string.Equals(v, f.Value, StringComparison.OrdinalIgnoreCase))
            : Flags.All(f => flags.TryGetValue(f.Name, out var v) && string.Equals(v, f.Value, StringComparison.OrdinalIgnoreCase)));
}

public enum FomodPluginType { Required, Recommended, Optional, CouldBeUsable, NotUsable }

public enum FomodGroupType { SelectExactlyOne, SelectAtMostOne, SelectAtLeastOne, SelectAll, SelectAny }

public sealed record FomodPlugin(string Name, string Description, string? Image, IReadOnlyList<FomodFile> Files, IReadOnlyList<FomodFlag> SetsFlags, FomodPluginType Type);

public sealed record FomodGroup(string Name, FomodGroupType Type, IReadOnlyList<FomodPlugin> Plugins);

public sealed record FomodStep(string Name, FomodCondition? Visible, IReadOnlyList<FomodGroup> Groups);

public sealed record FomodPattern(FomodCondition Condition, IReadOnlyList<FomodFile> Files);

/// <summary>Chosen options, by name: step → group → plugins. Same shape Vortex collections store ("choices").</summary>
public sealed record FomodChoice(string Step, string Group, IReadOnlyList<string> Plugins);

public sealed record FomodConfig(string Name, string? Image, IReadOnlyList<FomodFile> Required, IReadOnlyList<FomodStep> Steps, IReadOnlyList<FomodPattern> Conditional)
{
    /// <summary>The steps shown for the given choices (visibility depends on flags set by earlier steps).</summary>
    public IReadOnlyList<FomodStep> VisibleSteps(IReadOnlyList<FomodChoice> choices)
    {
        var result = new List<FomodStep>();
        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in Steps)
        {
            if (step.Visible is { } v && !v.IsMet(flags)) continue;
            result.Add(step);
            foreach (var p in Selected(step, choices)) foreach (var f in p.SetsFlags) flags[f.Name] = f.Value;
        }
        return result;
    }

    /// <summary>Plugins chosen in a step; groups without a stored choice use the default.</summary>
    public static IEnumerable<FomodPlugin> Selected(FomodStep step, IReadOnlyList<FomodChoice> choices)
    {
        foreach (var g in step.Groups)
        {
            var c = choices.FirstOrDefault(x => Eq(x.Step, step.Name) && Eq(x.Group, g.Name));
            var picked = c is null ? Defaults(g) : g.Plugins.Where(p => c.Plugins.Any(n => Eq(n, p.Name))).ToList();
            foreach (var p in picked.Concat(g.Plugins.Where(p => p.Type == FomodPluginType.Required)).Distinct()) yield return p;
        }
    }

    public static IReadOnlyList<FomodPlugin> Defaults(FomodGroup g)
    {
        var usable = g.Plugins.Where(p => p.Type != FomodPluginType.NotUsable).ToList();
        var preferred = usable.Where(p => p.Type is FomodPluginType.Required or FomodPluginType.Recommended).ToList();
        return g.Type switch
        {
            FomodGroupType.SelectAll => g.Plugins.ToList(),
            FomodGroupType.SelectExactlyOne => (preferred.Count > 0 ? preferred : usable).Take(1).ToList(),
            FomodGroupType.SelectAtLeastOne => preferred.Count > 0 ? preferred : usable.Take(1).ToList(),
            _ => preferred,
        };
    }

    /// <summary>All files to install for the choices, in install order (later ones win on the same destination).</summary>
    public IReadOnlyList<FomodFile> Resolve(IReadOnlyList<FomodChoice> choices)
    {
        var files = new List<FomodFile>(Required);
        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in VisibleSteps(choices))
            foreach (var p in Selected(step, choices))
            {
                files.AddRange(p.Files);
                foreach (var f in p.SetsFlags) flags[f.Name] = f.Value;
            }
        foreach (var pattern in Conditional)
            if (pattern.Condition.IsMet(flags)) files.AddRange(pattern.Files);
        return files.Select((f, i) => (f, i)).OrderBy(x => x.f.Priority).ThenBy(x => x.i).Select(x => x.f).ToList();
    }

    private static bool Eq(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}

public static class Fomod
{
    public const string ConfigFile = "fomod/ModuleConfig.xml";

    /// <summary>Folder (prefix) that contains fomod/ModuleConfig.xml, "" at the archive root.</summary>
    public static string RootOf(string configPath)
    {
        var p = configPath.Replace('\\', '/');
        var i = p.LastIndexOf("fomod/", StringComparison.OrdinalIgnoreCase);
        return i <= 0 ? "" : p[..(i - 1)];
    }

    public static FomodConfig Parse(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        var root = doc.Root ?? throw new FormatException("empty ModuleConfig.xml");
        var name = Child(root, "moduleName")?.Value.Trim() ?? "Installer";
        var image = Attr(Child(root, "moduleImage"), "path");
        var required = Files(Child(root, "requiredInstallFiles"));
        var steps = new List<FomodStep>();
        foreach (var s in Children(Child(root, "installSteps"), "installStep"))
        {
            var groups = new List<FomodGroup>();
            foreach (var g in Children(Child(s, "optionalFileGroups"), "group"))
            {
                var plugins = new List<FomodPlugin>();
                foreach (var p in Children(Child(g, "plugins"), "plugin"))
                {
                    var flags = Children(Child(p, "conditionFlags"), "flag").Select(f => new FomodFlag(Attr(f, "name") ?? "", f.Value.Trim())).ToList();
                    plugins.Add(new FomodPlugin(Attr(p, "name") ?? "", Child(p, "description")?.Value.Trim() ?? "", Attr(Child(p, "image"), "path"),
                        Files(Child(p, "files")), flags, PluginType(p)));
                }
                groups.Add(new FomodGroup(Attr(g, "name") ?? "", GroupType(Attr(g, "type")), plugins));
            }
            steps.Add(new FomodStep(Attr(s, "name") ?? "Options", Condition(Child(s, "visible")), groups));
        }
        var patterns = Children(Child(Child(root, "conditionalFileInstalls"), "patterns"), "pattern")
            .Select(p => new FomodPattern(Condition(Child(p, "dependencies")) ?? new FomodCondition(false, []), Files(Child(p, "files")))).ToList();
        return new FomodConfig(name, image, required, steps, patterns);
    }

    /// <summary>
    /// Maps archive entries through the chosen FOMOD files: returns (raw entry path, game-relative destination).
    /// Destination "" for a folder means the game root; for a file it means "same path as the source".
    /// </summary>
    public static IReadOnlyList<(string RawPath, string Target)> Map(IEnumerable<string> rawPaths, string fomodRoot, IReadOnlyList<FomodFile> files)
    {
        var prefix = fomodRoot.Length == 0 ? "" : fomodRoot.TrimEnd('/') + "/";
        var entries = rawPaths.Select(r => (Raw: r, Rel: r.Replace('\\', '/')))
            .Where(e => prefix.Length == 0 || e.Rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(e => (e.Raw, Rel: e.Rel[prefix.Length..])).ToList();
        var result = new Dictionary<string, (string Raw, string Target)>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in files)
        {
            var src = Norm(f.Source);
            var dst = Norm(f.Destination);
            if (f.IsFolder)
            {
                foreach (var e in entries.Where(e => src.Length == 0 || e.Rel.StartsWith(src + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    if (e.Rel.StartsWith("fomod/", StringComparison.OrdinalIgnoreCase)) continue;
                    var rest = src.Length == 0 ? e.Rel : e.Rel[(src.Length + 1)..];
                    var target = dst.Length == 0 ? rest : dst + "/" + rest;
                    result[target] = (e.Raw, target);
                }
            }
            else
            {
                var e = entries.FirstOrDefault(x => string.Equals(x.Rel, src, StringComparison.OrdinalIgnoreCase));
                if (e.Raw is null) continue;
                var toFolder = f.Destination.EndsWith('/') || f.Destination.EndsWith('\\');
                var fileName = src.Contains('/') ? src[(src.LastIndexOf('/') + 1)..] : src;
                var target = dst.Length == 0 ? src : toFolder ? dst + "/" + fileName : dst;
                result[target] = (e.Raw, target);
            }
        }
        return result.Values.ToList();
    }

    private static string Norm(string p) => p.Replace('\\', '/').Trim().Trim('/');

    private static FomodCondition? Condition(XElement? e)
    {
        if (e is null) return null;
        var deps = Child(e, "dependencies") ?? e;
        var or = string.Equals(Attr(deps, "operator"), "Or", StringComparison.OrdinalIgnoreCase);
        var flags = deps.Descendants().Where(d => d.Name.LocalName == "flagDependency")
            .Select(d => new FomodFlag(Attr(d, "flag") ?? "", Attr(d, "value") ?? "")).ToList();
        return new FomodCondition(or, flags);
    }

    private static FomodPluginType PluginType(XElement plugin)
    {
        var td = Child(plugin, "typeDescriptor");
        var name = Attr(Child(td, "type"), "name") ?? Attr(Child(Child(td, "dependencyType"), "defaultType"), "name") ?? "Optional";
        return Enum.TryParse<FomodPluginType>(name, true, out var t) ? t : FomodPluginType.Optional;
    }

    private static FomodGroupType GroupType(string? t) =>
        Enum.TryParse<FomodGroupType>(t, true, out var g) ? g : FomodGroupType.SelectAny;

    private static List<FomodFile> Files(XElement? e)
    {
        if (e is null) return [];
        return e.Elements().Where(x => x.Name.LocalName is "file" or "folder")
            .Select(x => new FomodFile(Attr(x, "source") ?? "", Attr(x, "destination") ?? "", x.Name.LocalName == "folder",
                int.TryParse(Attr(x, "priority"), out var pr) ? pr : 0)).ToList();
    }

    private static XElement? Child(XElement? e, string name) => e?.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
    private static IEnumerable<XElement> Children(XElement? e, string name) => e?.Elements().Where(x => string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)) ?? [];
    private static string? Attr(XElement? e, string name) => e?.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;
}
