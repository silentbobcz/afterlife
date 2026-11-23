using System.Text;
using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>
/// The question about a failed redscript compilation that the user copies into an AI chat: game version, the mods
/// the errors point to with their versions, the error lines and the frameworks in use. Paths in the log carry the
/// user's name, so the whole text passes <see cref="LogPrivacy.Scrub(string)"/>.
/// </summary>
public static class RedscriptPrompt
{
    public const int MaxErrors = 20;

    /// <param name="frameworks">Lines such as "Codeware 1.15.0" (see <see cref="Frameworks"/>).</param>
    /// <param name="scrub">The privacy filter; <see cref="LogPrivacy.Scrub(string)"/> when null.</param>
    public static string Build(RedscriptReport report, string? gameVersion, IReadOnlyList<string> frameworks, Func<string, string>? scrub = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("The redscript compilation of my Cyberpunk 2077 fails, so no script mod works. Which mod causes it, and what should I do (update it, remove it, or install a missing requirement)?");
        sb.AppendLine();
        sb.AppendLine($"Game version: {gameVersion ?? "unknown"}");
        sb.AppendLine($"Result in {RedscriptLog.RelativePath}: {Verdict(report)}");

        if (report.LibraryCulprits.Count > 0 || report.ForeignCulprits.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Mods the errors point to:");
            foreach (var c in report.LibraryCulprits)
                sb.AppendLine($"- {c.Name} {c.Version} ({(c.Enabled ? "enabled" : "disabled")} in my mod manager{(c.NamedInLog ? ", named by redscript" : "")}){Files(c.Files)}");
            foreach (var c in report.ForeignCulprits)
                sb.AppendLine($"- {c.Name} (installed by hand, version unknown{(c.NamedInLog ? ", named by redscript" : "")}){Files(c.Files)}");
        }
        if (report.Log.CausedBy.Count > 0)
            sb.AppendLine($"redscript names as the cause: {string.Join(", ", report.Log.CausedBy)}");

        var errors = report.Log.Errors;
        if (errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Errors from the log:");
            sb.AppendLine("```");
            foreach (var e in errors.Take(MaxErrors)) sb.AppendLine(e.Text);
            if (errors.Count > MaxErrors) sb.AppendLine($"({errors.Count - MaxErrors} more errors not shown)");
            sb.AppendLine("```");
        }
        if (!report.Log.Finished && report.Status != RedscriptLogStatus.Missing)
            sb.AppendLine("The log ends early (it has no final result).");

        sb.AppendLine();
        sb.AppendLine("Frameworks enabled:");
        if (frameworks.Count == 0) sb.AppendLine("- none found");
        foreach (var f in frameworks) sb.AppendLine($"- {f}");
        var text = sb.ToString().TrimEnd() + Environment.NewLine;
        return scrub is null ? LogPrivacy.Scrub(text) : scrub(text);
    }

    /// <summary>
    /// Frameworks the profile switches on (with the version of their package) and those installed by hand (with the
    /// version read from their binary), by display name.
    /// </summary>
    public static IReadOnlyList<string> Frameworks(ProfileState profile, IReadOnlyCollection<PackageManifest> packages,
        IReadOnlySet<string> externalCapabilities, IReadOnlyDictionary<string, string?> externalVersions)
    {
        var lines = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in packages.Where(p => profile.IsEnabled(p.PackageId)))
            foreach (var cap in p.Provides.Where(c => c.StartsWith("framework:", StringComparison.Ordinal)))
                if (seen.Add(cap)) lines.Add($"{Capabilities.DisplayName(cap)} {p.Version}".TrimEnd());
        foreach (var cap in externalCapabilities.Where(c => c.StartsWith("framework:", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
            if (seen.Add(cap))
                lines.Add($"{Capabilities.DisplayName(cap)} {(externalVersions.TryGetValue(cap, out var v) ? v : null) ?? ""} (installed by hand)".Replace("  ", " "));
        return lines.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Verdict(RedscriptReport r) => r.Status switch
    {
        RedscriptLogStatus.Failed => "compilation failed",
        RedscriptLogStatus.Succeeded => "compilation succeeded",
        RedscriptLogStatus.Stale => "the log is older than my last change of mods (" + r.Log.Outcome.ToString().ToLowerInvariant() + " then)",
        RedscriptLogStatus.Missing => "no log found",
        RedscriptLogStatus.Unreadable => "the log could not be read",
        _ => "no result in the log",
    };

    private static string Files(IReadOnlyList<string> files) =>
        files.Count == 0 ? "" : ": " + string.Join(", ", files.Take(5)) + (files.Count > 5 ? $" and {files.Count - 5} more" : "");
}
