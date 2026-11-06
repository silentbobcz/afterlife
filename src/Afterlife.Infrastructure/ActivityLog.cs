using System.Text;
using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>
/// P113: optional plain-text record of what AFTERLIFE does (switches, imports, every planned file change with its
/// reason, blockers, results). Off by default; switched on in Settings. One file per day next to the error logs,
/// so "Open log folder" shows both. Never throws: a failing log must not stop the work it describes.
/// </summary>
public static class ActivityLog
{
    private static readonly object Gate = new();
    private static string? _directory;

    /// <summary>Set once at start-up from the saved settings; changed live from the Settings overlay.</summary>
    public static bool Enabled { get; set; }

    /// <summary>Folder for the activity files; defaults to %LOCALAPPDATA%\AFTERLIFE\logs.</summary>
    public static string Directory
    {
        get => _directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AFTERLIFE", "logs");
        set => _directory = value;
    }

    public static string CurrentFile => Path.Combine(Directory, $"activity-{DateTime.Now:yyyyMMdd}.log");

    /// <summary>Writes one line: time, category, message. Multi-line messages are indented so the file stays readable.</summary>
    public static void Write(string category, string message)
    {
        if (!Enabled) return;
        try
        {
            var text = message.Replace("\r\n", "\n").Replace("\n", "\n    ");
            var line = $"{DateTime.Now:HH:mm:ss.fff}  {category,-9} {text}{Environment.NewLine}";
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.AppendAllText(CurrentFile, line, new UTF8Encoding(false));
            }
        }
        catch (Exception) { /* logging never breaks the operation it describes */ }
    }

    /// <summary>Several lines under one category (plan steps, lists).</summary>
    public static void Write(string category, IEnumerable<string> lines)
    {
        if (!Enabled) return;
        Write(category, string.Join("\n", lines));
    }

    /// <summary>Human summary of a plan: counts, then every step with kind, path, owner and reason.</summary>
    public static void Plan(string title, Core.Plan plan)
    {
        if (!Enabled) return;
        var lines = new List<string> { $"{title}: {plan.Steps.Count} step(s), {plan.Blockers.Count} blocker(s), {plan.Warnings.Count} warning(s)" };
        lines.AddRange(plan.Blockers.Select(b => $"BLOCKED  {b.Kind} {b.Path ?? ""} [{b.PackageId ?? "-"}] {b.Message}"));
        lines.AddRange(plan.Steps.Select(s => $"{s.Kind,-7} {s.Path} [{s.Owner ?? "-"}] {s.Reason}"));
        Write("plan", lines);
    }
}
