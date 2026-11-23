using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>What the redscript log says, as seen from the library.</summary>
public enum RedscriptLogStatus
{
    /// <summary>The game has not written the log yet.</summary>
    Missing,
    /// <summary>The log exists but could not be read.</summary>
    Unreadable,
    /// <summary>The log is older than the last apply (or the given launch): it describes another set of mods.</summary>
    Stale,
    /// <summary>The log has no verdict and no errors (empty or cut early).</summary>
    Unknown,
    Succeeded,
    Failed,
}

/// <summary>How an error was tied to a mod of the library.</summary>
public enum RedscriptMatch
{
    /// <summary>The script is a file AFTERLIFE deployed for that mod.</summary>
    DeployedFile,
    /// <summary>The script lies in an <c>r6/scripts</c> folder (or a REDmod folder) where AFTERLIFE deployed files of that mod.</summary>
    ScriptFolder,
}

/// <param name="Files">Game-relative scripts with errors (the path from the log when it lies outside the game folder).</param>
/// <param name="NamedInLog">The mod's folder is in the "caused by" list of the log.</param>
/// <param name="Enabled">Switched on in the profile the report was made for.</param>
public sealed record RedscriptLibraryCulprit(
    string PackageId,
    string Name,
    string Version,
    bool Enabled,
    bool NamedInLog,
    RedscriptMatch Match,
    IReadOnlyList<string> Files,
    IReadOnlyList<RedscriptError> Errors);

/// <summary>Scripts that belong to no mod of the library: installed by hand (or by another tool).</summary>
/// <param name="Name">The <c>r6/scripts</c> folder, the file name for a script directly in it, or the name from the log.</param>
public sealed record RedscriptForeignCulprit(
    string Name,
    bool NamedInLog,
    IReadOnlyList<string> Files,
    IReadOnlyList<RedscriptError> Errors);

/// <summary>Everything a window needs to explain a failed redscript compilation.</summary>
/// <param name="LogWritten">Last write of the log (UTC); null when it is missing.</param>
/// <param name="Log">The parsed log; <see cref="RedscriptLogResult.Empty"/> when it is missing or unreadable.</param>
/// <param name="UnplacedErrors">Errors that name no script.</param>
public sealed record RedscriptReport(
    RedscriptLogStatus Status,
    string LogPath,
    DateTimeOffset? LogWritten,
    RedscriptLogResult Log,
    string ProfileId,
    string ProfileName,
    IReadOnlyList<RedscriptLibraryCulprit> LibraryCulprits,
    IReadOnlyList<RedscriptForeignCulprit> ForeignCulprits,
    IReadOnlyList<RedscriptError> UnplacedErrors)
{
    /// <summary>Ready-made question for an AI chat, already passed through <see cref="LogPrivacy.Scrub(string)"/>.</summary>
    public string AiPrompt { get; init; } = "";

    public bool HasCulprits => LibraryCulprits.Count > 0 || ForeignCulprits.Count > 0;
}

/// <summary>
/// Ties the errors of the redscript log to mods: first by the exact path against the files AFTERLIFE deployed
/// (the ledger, only read), then by the folder in <c>r6/scripts</c>. What belongs to no mod of the library is
/// reported as installed by hand. No guessing by similar names.
/// </summary>
public static class RedscriptDiagnosis
{
    /// <summary>
    /// The one call for a window: reads the log of the game, ties it to the library and the profile (the selected
    /// one when null) and writes the AI question. Changes nothing.
    /// </summary>
    /// <param name="launchedAt">When the game was last started from AFTERLIFE, if known: an older log is stale.</param>
    public static RedscriptReport AnalyzeRedscript(this InstallCoordinator c, ProfileState? profile = null, DateTimeOffset? launchedAt = null)
    {
        profile ??= c.SelectedProfile;
        var packages = c.Store.GetPackages();
        DateTimeOffset? notBefore = c.Store.GetLastGeneration()?.CreatedAt;
        if (launchedAt is { } l && (notBefore is null || l > notBefore)) notBefore = l;
        var report = Analyze(c.GameRoot, profile, packages, c.Store.GetLedger(), notBefore);

        c.BuildChanged(out var fingerprint);
        var scan = c.CachedScan();
        var frameworks = RedscriptPrompt.Frameworks(profile, packages, scan.ExternalCapabilities, scan.ExternalVersions);
        return report with { AiPrompt = RedscriptPrompt.Build(report, fingerprint?.Display, frameworks) };
    }

    /// <summary>Reads <see cref="RedscriptLog.RelativePath"/> under <paramref name="gameRoot"/> and ties it to the library.</summary>
    /// <param name="notBefore">A log written before this moment is <see cref="RedscriptLogStatus.Stale"/>.</param>
    public static RedscriptReport Analyze(string gameRoot, ProfileState profile, IReadOnlyCollection<PackageManifest> packages,
        IReadOnlyDictionary<string, LedgerEntry> ledger, DateTimeOffset? notBefore)
    {
        var path = GamePath.Parse(RedscriptLog.RelativePath).ToOsPath(gameRoot);
        RedscriptLogStatus status;
        DateTimeOffset? written = null;
        var log = RedscriptLogResult.Empty;
        if (!File.Exists(path)) status = RedscriptLogStatus.Missing;
        else
        {
            try
            {
                written = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
                // the game may still hold the file open for writing
                using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                log = RedscriptLog.Parse(ms.ToArray());
                status = written < notBefore ? RedscriptLogStatus.Stale
                    : log.Outcome switch
                    {
                        RedscriptOutcome.Failed => RedscriptLogStatus.Failed,
                        RedscriptOutcome.Succeeded => RedscriptLogStatus.Succeeded,
                        _ => RedscriptLogStatus.Unknown,
                    };
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                status = RedscriptLogStatus.Unreadable;
            }
        }
        return Match(log, status, path, written, gameRoot, profile, packages, ledger);
    }

    private sealed class LibraryAcc(PackageManifest package, RedscriptMatch match)
    {
        public PackageManifest Package { get; } = package;
        public RedscriptMatch Match { get; set; } = match;
        public bool Named { get; set; }
        public List<string> Files { get; } = [];
        public List<RedscriptError> Errors { get; } = [];
    }

    private sealed class ForeignAcc(string name)
    {
        public string Name { get; } = name;
        public bool Named { get; set; }
        public List<string> Files { get; } = [];
        public List<RedscriptError> Errors { get; } = [];
    }

    /// <summary>Ties an already parsed log to the library (pure apart from its inputs).</summary>
    public static RedscriptReport Match(RedscriptLogResult log, RedscriptLogStatus status, string logPath, DateTimeOffset? written,
        string gameRoot, ProfileState profile, IReadOnlyCollection<PackageManifest> packages, IReadOnlyDictionary<string, LedgerEntry> ledger)
    {
        var byId = packages.GroupBy(p => p.PackageId).ToDictionary(g => g.Key, g => g.First());
        var owners = new Dictionary<string, string>(StringComparer.Ordinal); // path key → package id
        var folderOwners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); // "r6/scripts/X" or "mods/X" → package ids
        foreach (var e in ledger.Values)
        {
            if (e.Kind != ArtifactKind.Payload || !byId.ContainsKey(e.Owner)) continue;
            owners[e.Path.ToUpperInvariant()] = e.Owner;
            var folder = FolderKey(e.Path);
            if (folder is null) continue;
            if (!folderOwners.TryGetValue(folder, out var list)) folderOwners[folder] = list = [];
            if (!list.Contains(e.Owner)) list.Add(e.Owner);
        }

        var library = new List<LibraryAcc>();
        var foreign = new List<ForeignAcc>();
        var unplaced = new List<RedscriptError>();

        LibraryAcc Lib(string packageId, RedscriptMatch match)
        {
            var acc = library.FirstOrDefault(a => a.Package.PackageId == packageId);
            if (acc is null) library.Add(acc = new LibraryAcc(byId[packageId], match));
            else if (match == RedscriptMatch.DeployedFile) acc.Match = match;
            return acc;
        }

        ForeignAcc Foreign(string name)
        {
            var acc = foreign.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (acc is null) foreign.Add(acc = new ForeignAcc(name));
            return acc;
        }

        static void Note(List<string> files, List<RedscriptError> errors, string file, RedscriptError error)
        {
            if (!files.Contains(file, StringComparer.OrdinalIgnoreCase)) files.Add(file);
            errors.Add(error);
        }

        foreach (var error in log.Errors)
        {
            if (error.File is null) { unplaced.Add(error); continue; }
            var rel = RedscriptLog.ToGamePath(error.File, gameRoot);
            var shown = rel ?? error.File;
            if (rel is not null && owners.TryGetValue(rel.ToUpperInvariant(), out var owner))
            {
                var acc = Lib(owner, RedscriptMatch.DeployedFile);
                Note(acc.Files, acc.Errors, shown, error);
                continue;
            }
            var folder = rel is null ? null : FolderKey(rel);
            if (folder is not null && folderOwners.TryGetValue(folder, out var ids))
            {
                foreach (var id in ids)
                {
                    var acc = Lib(id, RedscriptMatch.ScriptFolder);
                    Note(acc.Files, acc.Errors, shown, error);
                }
                continue;
            }
            var f = Foreign(FileName(folder ?? shown));
            Note(f.Files, f.Errors, shown, error);
        }

        foreach (var name in log.CausedBy)
        {
            var ids = new List<string>();
            foreach (var key in new[] { "r6/scripts/" + name, "mods/" + name })
                if (folderOwners.TryGetValue(key, out var list)) ids.AddRange(list.Where(id => !ids.Contains(id)));
            if (owners.TryGetValue(("r6/scripts/" + name).ToUpperInvariant(), out var fileOwner) && !ids.Contains(fileOwner)) ids.Add(fileOwner);
            if (ids.Count > 0)
                foreach (var id in ids) Lib(id, RedscriptMatch.ScriptFolder).Named = true;
            else Foreign(name).Named = true;
        }

        return new RedscriptReport(status, logPath, written, log, profile.Id, profile.Name,
            library.Select(a => new RedscriptLibraryCulprit(a.Package.PackageId, a.Package.ModName, a.Package.Version,
                profile.IsEnabled(a.Package.PackageId), a.Named, a.Match, a.Files, a.Errors)).ToList(),
            foreign.Select(a => new RedscriptForeignCulprit(a.Name, a.Named, a.Files, a.Errors)).ToList(),
            unplaced);
    }

    /// <summary>"r6/scripts/X" for a script in a module folder, "mods/X" for a REDmod; null otherwise.</summary>
    private static string? FolderKey(string gamePath)
    {
        var p = gamePath.Replace('\\', '/');
        var module = RedscriptLog.ScriptFolder(p);
        if (module is not null) return "r6/scripts/" + module;
        if (!p.StartsWith("mods/", StringComparison.OrdinalIgnoreCase)) return null;
        var slash = p.IndexOf('/', "mods/".Length);
        return slash > "mods/".Length ? p[..slash] : null;
    }

    private static string FileName(string path)
    {
        var p = path.Replace('\\', '/');
        return p[(p.LastIndexOf('/') + 1)..];
    }
}
