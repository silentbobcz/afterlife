namespace Afterlife.Core;

public sealed record ArchiveEntryInfo(string RawPath, long Size);

public enum ImportStatus { Ok, NeedsChoice, Rejected, NeedsFomod }

public sealed record MappedEntry(string RawPath, GamePath Target, long Size);

public sealed record ImportAnalysis(
    ImportStatus Status,
    IReadOnlyList<MappedEntry> Mapped,
    IReadOnlyList<string> Ignored,
    IReadOnlyList<string> RootCandidates,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Provides,
    IReadOnlyList<RequirementSpec> Requirements)
{
    /// <summary>Rejected only because a framework contract is unverified; an explicit test import is possible.</summary>
    public bool UnverifiedFrameworkOnly { get; init; }
    /// <summary>R3: archive path of fomod/ModuleConfig.xml when the mod has an installer with options.</summary>
    public string? FomodConfigPath { get; init; }
}

public sealed record ImportLimits(int MaxFiles = 20_000, long MaxTotalBytes = 8L * 1024 * 1024 * 1024)
{
    public static ImportLimits Default { get; } = new();
}

public sealed record ImportOptions(string? ForcedRoot = null, bool AllowUnverifiedFrameworks = false)
{
    /// <summary>R3: files chosen in the FOMOD installer (relative to the folder that contains fomod/).</summary>
    public IReadOnlyList<FomodFile>? FomodFiles { get; init; }
}

/// <summary>
/// Decides where files from an archive go in the game folder. Pure logic, no IO.
/// Normative rules: AFTERLIFE-architektura-v2.1.md, "Import a bezpečnost" and "Podporované typy modů v MVP".
/// </summary>
public static class ImportAnalyzer
{
    private static readonly string[] KnownTopDirs = { "ARCHIVE", "R6", "BIN", "RED4EXT", "ENGINE" };
    private static readonly string[] DocExtensions = { ".txt", ".md", ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".url", ".html", ".htm", ".rtf" };
    private static readonly string[] ScriptExtensions = { ".bat", ".cmd", ".ps1", ".vbs", ".msi" };

    public static ImportAnalysis Analyze(IReadOnlyList<ArchiveEntryInfo> entries, ImportOptions? options = null, ImportLimits? limits = null)
    {
        options ??= new ImportOptions();
        limits ??= ImportLimits.Default;
        var problems = new List<string>();
        var warnings = new List<string>();

        if (entries.Count == 0) return Reject("archive contains no files");
        if (entries.Count > limits.MaxFiles) return Reject($"archive has {entries.Count} files (limit {limits.MaxFiles})");
        var declared = entries.Sum(e => Math.Max(0, e.Size));
        if (declared > limits.MaxTotalBytes) return Reject($"archive declares {declared} bytes (limit {limits.MaxTotalBytes})");

        // 1. Every entry path must be safe, and must stay unique after Windows normalization.
        var parsed = new List<(ArchiveEntryInfo Entry, GamePath Path)>();
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            if (!GamePath.TryCreate(e.RawPath, out var p, out var err))
            {
                problems.Add($"unsafe path '{e.RawPath}': {err}");
                continue;
            }
            if (seen.TryGetValue(p.Key, out var other))
            {
                problems.Add($"paths collide on Windows: '{other}' and '{e.RawPath}'");
                continue;
            }
            seen[p.Key] = e.RawPath;
            parsed.Add((e, p));
        }
        if (problems.Count > 0) return new ImportAnalysis(ImportStatus.Rejected, [], [], [], problems, warnings, [], []);

        if (parsed.Any(x => ScriptExtensions.Contains(x.Path.Extension)))
            warnings.Add("archive contains scripts or installers; AFTERLIFE never runs them");

        // 2. Unsupported installer formats.
        bool hasFomod = parsed.Any(x => x.Path.Key.EndsWith("FOMOD/MODULECONFIG.XML", StringComparison.Ordinal));
        if (hasFomod && options.ForcedRoot is null)
        {
            var config = parsed.Where(x => x.Path.Key.EndsWith("FOMOD/MODULECONFIG.XML", StringComparison.Ordinal)).OrderBy(x => x.Path.Value.Length).First();
            if (options.FomodFiles is null)
                return new ImportAnalysis(ImportStatus.NeedsFomod, [], [], [], ["the mod has an installer with options"], warnings, [], []) { FomodConfigPath = config.Entry.RawPath };
            // R3: the chosen files become the archive content, at their game paths
            var mappedFomod = Fomod.Map(parsed.Select(x => x.Entry.RawPath), Fomod.RootOf(config.Path.Value), options.FomodFiles);
            if (mappedFomod.Count == 0) return Reject("the chosen installer options contain no files");
            var byRaw = parsed.ToDictionary(x => x.Entry.RawPath, x => x.Entry, StringComparer.Ordinal);
            var remapped = new List<(ArchiveEntryInfo Entry, GamePath Path)>();
            foreach (var (raw, target) in mappedFomod)
            {
                if (!GamePath.TryCreate(target, out var gp, out var why)) return Reject($"installer writes to an invalid path '{target}': {why}");
                remapped.Add((byRaw[raw] with { RawPath = raw }, gp));
            }
            parsed = remapped;
            hasFomod = false;
            options = options with { ForcedRoot = "" };
        }
        // REDmod without the mods/ folder ("Name/info.json" next to archives/, scripts/, tweaks/ or customSounds/): put it under mods/
        if (!parsed.Any(x => IsRedmodInfo(x.Path)))
        {
            var bare = BareRedmodRoots(parsed.Select(x => x.Path));
            if (bare.Count == 1 && RootCandidates(parsed.Select(x => x.Path)).Count == 0)
            {
                var (prefix, name) = bare[0];
                var start = prefix.Length == 0 ? "" : prefix + "/";
                parsed = parsed.Where(x => x.Path.Value.StartsWith(start + name + "/", StringComparison.OrdinalIgnoreCase))
                    .Select(x => (x.Entry, GamePath.Parse(start + "mods/" + x.Path.Value[start.Length..]))).ToList();
            }
        }
        bool hasRedmod = parsed.Any(x => IsRedmodInfo(x.Path));
        if (hasFomod && options.ForcedRoot is null)
            return Reject("FOMOD installers are not supported; choose one plain folder manually (expert import)");

        // 3. Root detection.
        IReadOnlyList<(ArchiveEntryInfo Entry, GamePath Path)> scope = parsed;
        string root;
        if (options.ForcedRoot is not null)
        {
            root = NormalizeRoot(options.ForcedRoot);
            scope = parsed.Where(x => root.Length == 0 || x.Path.Key.StartsWith(root.ToUpperInvariant() + "/", StringComparison.Ordinal)).ToList();
            if (scope.Count == 0) return Reject($"no files under chosen folder '{options.ForcedRoot}'");
        }
        else
        {
            var candidates = RootCandidates(parsed.Where(x => !IsRedmodPath(x.Path)).Select(x => x.Path));
            if (hasRedmod)
            {
                // R2: REDmods are supported: mods/<Name>/… goes to the game as is; legacy folders next to it too
                var redRoots = parsed.Where(x => IsRedmodInfo(x.Path)).Select(x => RedmodPrefix(x.Path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (redRoots.Count > 1)
                    return new ImportAnalysis(ImportStatus.NeedsChoice, [], [], redRoots.Order(StringComparer.Ordinal).ToList(),
                        ["archive contains several install variants; choose one"], warnings, [], []);
                if (candidates.Count == 0 || (candidates.Count == 1 && string.Equals(candidates[0], redRoots[0], StringComparison.OrdinalIgnoreCase)))
                    candidates = [redRoots[0]];
                else
                    return new ImportAnalysis(ImportStatus.NeedsChoice, [], [], candidates.Append(redRoots[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        ["archive contains several install variants; choose one"], warnings, [], []);
            }
            if (candidates.Count > 1)
                return new ImportAnalysis(ImportStatus.NeedsChoice, [], [], candidates,
                    ["archive contains several install variants; choose one"], warnings, [], []);
            if (candidates.Count == 1) root = candidates[0];
            else
            {
                // Loose .archive / .xl files without game folder structure.
                var content = parsed.Where(x => !DocExtensions.Contains(x.Path.Extension)).ToList();
                if (content.Count > 0 && content.All(x => x.Path.Extension is ".archive" or ".xl"))
                {
                    var dirs = content.Select(x => x.Path.ParentValue ?? "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    if (dirs.Count > 1)
                        return new ImportAnalysis(ImportStatus.NeedsChoice, [], [], dirs.Order(StringComparer.Ordinal).ToList(),
                            ["archive contains several folders with .archive files; choose one"], warnings, [], []);
                    return Finish(content.Select(x => new MappedEntry(x.Entry.RawPath, GamePath.Parse("archive/pc/mod/" + x.Path.FileName), x.Entry.Size)).ToList(),
                        parsed.Except(content).Select(x => x.Entry.RawPath).ToList(), warnings, options);
                }
                return Reject("unknown folder layout; choose the folder that corresponds to the game root");
            }
        }

        // 4. Map entries under the root; only known top-level game folders are deployed.
        var mapped = new List<MappedEntry>();
        var ignored = new List<string>();
        var rootPrefix = root.Length == 0 ? "" : root + "/";
        var looseArchives = new List<(ArchiveEntryInfo, GamePath)>();
        foreach (var (entry, path) in scope)
        {
            var rel = path.Value[rootPrefix.Length..];
            var target = GamePath.Parse(rel);
            if (IsRedmodPath(target))
            {
                // only real REDmod folders (mods/<Name>/…), never loose files in mods/
                if (target.Value.Count(ch => ch == '/') >= 2 && !DocExtensions.Contains(target.Extension) || target.FileName.Equals("info.json", StringComparison.OrdinalIgnoreCase))
                    mapped.Add(new MappedEntry(entry.RawPath, target, entry.Size));
                else ignored.Add(entry.RawPath);
                continue;
            }
            if (target.Key == ModlistFormat.RelativePath.ToUpperInvariant())
            {
                ignored.Add(entry.RawPath);
                warnings.Add("modlist.txt inside the archive was ignored; archive order is managed per profile");
                continue;
            }
            if (KnownTopDirs.Contains(target.FirstSegmentKey))
            {
                if (target.FirstSegmentKey == "BIN" && !target.IsUnder("bin/x64"))
                {
                    ignored.Add(entry.RawPath);
                    continue;
                }
                mapped.Add(new MappedEntry(entry.RawPath, target, entry.Size));
            }
            else if (options.ForcedRoot is not null && target.Extension is ".archive" or ".xl" && !target.Value.Contains('/'))
                looseArchives.Add((entry, target));
            else ignored.Add(entry.RawPath);
        }
        foreach (var (entry, target) in looseArchives)
            mapped.Add(new MappedEntry(entry.RawPath, GamePath.Parse("archive/pc/mod/" + target.FileName), entry.Size));

        if (mapped.Count == 0) return Reject("nothing to install under the chosen folder");
        return Finish(mapped, ignored, warnings, options);
    }

    private static ImportAnalysis Finish(List<MappedEntry> mapped, List<string> ignored, List<string> warnings, ImportOptions options)
    {
        var dup = mapped.GroupBy(m => m.Target.Key).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) return Reject($"two files map to the same target '{dup.First().Target}'");

        var targets = mapped.Select(m => m.Target).ToList();
        var provides = FrameworkCatalog.DetectProvided(targets);
        foreach (var cap in provides)
        {
            var contract = FrameworkCatalog.Find(cap)!;
            if (!contract.ManagedInMvp)
                return Reject($"AFTERLIFE cannot install {Capabilities.DisplayName(cap)} yet; install it by hand");
            // P36: frameworks install like any other mod; their own runtime files are tidied up when switched off (P37)
        }
        var reqs = FrameworkCatalog.InferRequirements(targets, provides);
        if (targets.Any(t => t.Extension == ".archive"))
            warnings.Add("files inside .archive packs are not compared with other mods");
        return new ImportAnalysis(ImportStatus.Ok, mapped.OrderBy(m => m.Target).ToList(), ignored, [], [], warnings, provides, reqs);
    }

    private static ImportAnalysis Reject(string reason) =>
        new(ImportStatus.Rejected, [], [], [], [reason], [], [], []);

    private static string NormalizeRoot(string root)
    {
        var r = root.Replace('\\', '/').Trim('/');
        if (r.Length == 0) return "";
        return GamePath.Parse(r).Value;
    }

    private static bool IsRedmodInfo(GamePath p)
    {
        var segs = p.Key.Split('/');
        for (int i = 0; i + 2 < segs.Length; i++)
            if (segs[i] == "MODS" && segs[i + 2] == "INFO.JSON" && i + 3 == segs.Length) return true;
        return false;
    }

    private static bool IsRedmodPath(GamePath p) => p.FirstSegmentKey == "MODS";

    /// <summary>Prefix before "mods/" of a mods/&lt;Name&gt;/info.json path ("" at the archive root).</summary>
    private static string RedmodPrefix(GamePath p)
    {
        var segs = p.Value.Split('/');
        for (int i = 0; i + 2 < segs.Length; i++)
            if (segs[i].Equals("mods", StringComparison.OrdinalIgnoreCase) && segs[i + 2].Equals("info.json", StringComparison.OrdinalIgnoreCase) && i + 3 == segs.Length)
                return string.Join('/', segs[..i]);
        return "";
    }

    private static readonly string[] RedmodContentDirs = { "ARCHIVES", "SCRIPTS", "TWEAKS", "CUSTOMSOUNDS" };

    /// <summary>"Name/info.json" with REDmod content folders next to it: (prefix, Name).</summary>
    private static List<(string Prefix, string Name)> BareRedmodRoots(IEnumerable<GamePath> paths)
    {
        var list = paths.ToList();
        var result = new List<(string, string)>();
        foreach (var info in list.Where(p => p.FileName.Equals("info.json", StringComparison.OrdinalIgnoreCase)))
        {
            var segs = info.Value.Split('/');
            if (segs.Length < 2) continue;
            var dir = string.Join('/', segs[..^1]);
            var hasContent = list.Any(p => p.Value.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase)
                && RedmodContentDirs.Contains(p.Value[(dir.Length + 1)..].Split('/')[0].ToUpperInvariant()));
            if (hasContent) result.Add((string.Join('/', segs[..^2]), segs[^2]));
        }
        return result;
    }

    /// <summary>Directory prefixes (possibly empty) that directly contain a known game top-level folder.</summary>
    private static List<string> RootCandidates(IEnumerable<GamePath> paths)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            var segs = p.Value.Split('/');
            for (int i = 0; i < segs.Length - 1; i++)
            {
                if (KnownTopDirs.Contains(segs[i].ToUpperInvariant()))
                {
                    roots.Add(string.Join('/', segs.Take(i)));
                    break;
                }
            }
        }
        // A candidate nested inside another candidate is the same install (e.g. "x" and "x/archive/..."): keep shallowest.
        var list = roots.OrderBy(r => r.Length).ToList();
        var result = new List<string>();
        foreach (var r in list)
            if (!result.Any(k => k.Length == 0 ? false : r.StartsWith(k + "/", StringComparison.OrdinalIgnoreCase)))
                result.Add(r);
        return result.Order(StringComparer.Ordinal).ToList();
    }
}
