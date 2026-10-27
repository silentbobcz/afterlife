using System.Text.Json;

namespace Afterlife.Infrastructure;

/// <summary>
/// Where the library lives. Used by every AFTERLIFE start so all of them open the same install.
/// Stored in %APPDATA%\AFTERLIFE\settings.json; AFTERLIFE_LIBRARY overrides it.
/// </summary>
public sealed record AppSettings(string? LibraryRoot)
{
    /// <summary>Turns off decorative motion (scan sweep, breach animation, travelling dots). Spec v2.1: Reduce motion.</summary>
    public bool ReduceMotion { get; init; }

    public const string PortableLibraryFolderName = "AFTERLIFE Library";

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AFTERLIFE");

    public static string SettingsFile => Path.Combine(SettingsDirectory, "settings.json");

    public static string DefaultLibraryRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AFTERLIFE", "Library");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile), Json.Options) ?? new AppSettings((string?)null);
        }
        catch (JsonException) { /* corrupt settings: fall back to defaults, never crash */ }
        return new AppSettings((string?)null);
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var tmp = SettingsFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json.Options));
        File.Move(tmp, SettingsFile, overwrite: true);
    }

    /// <summary>Explicit argument → environment variable → saved settings → library next to the exe → default.</summary>
    public static string ResolveLibrary(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot)) return Path.GetFullPath(explicitRoot);
        var env = Environment.GetEnvironmentVariable("AFTERLIFE_LIBRARY");
        if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);
        var saved = Load().LibraryRoot;
        if (!string.IsNullOrWhiteSpace(saved)) return saved;
        var portable = Path.Combine(AppContext.BaseDirectory, PortableLibraryFolderName);
        return IsInitialized(portable) ? portable : DefaultLibraryRoot;
    }

    /// <summary>
    /// Library suggested on first run (P2): a folder next to AFTERLIFE.exe when that place is usable,
    /// otherwise %LOCALAPPDATA%. Returns the reason when the exe folder is not used.
    /// </summary>
    public static (string Path, string? FallbackReason) SuggestLibrary(string? gameRoot, string? exeDirectory = null, Func<string, bool>? canWrite = null)
    {
        var exeDir = Path.GetFullPath(exeDirectory ?? AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var candidate = Path.Combine(exeDir, PortableLibraryFolderName);
        canWrite ??= CanWrite;
        string? reason = null;
        if (!string.IsNullOrWhiteSpace(gameRoot))
        {
            var game = Path.GetFullPath(gameRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (exeDir.Equals(game, StringComparison.OrdinalIgnoreCase) || exeDir.StartsWith(game + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                reason = "AFTERLIFE.exe is inside the game folder";
        }
        foreach (var special in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.Windows })
        {
            var sp = Environment.GetFolderPath(special);
            if (reason is null && sp.Length > 0 && exeDir.StartsWith(sp, StringComparison.OrdinalIgnoreCase)) reason = "AFTERLIFE.exe is in a protected Windows folder";
        }
        if (reason is null && exeDir.Contains(Path.DirectorySeparatorChar + "Temp", StringComparison.OrdinalIgnoreCase)) reason = "AFTERLIFE.exe runs from a temporary folder (unpacked zip?)";
        if (reason is null && !canWrite(exeDir)) reason = "the folder of AFTERLIFE.exe is not writable";
        return reason is null ? (candidate, null) : (DefaultLibraryRoot, reason);
    }

    private static bool CanWrite(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".afterlife-write-test-" + Guid.NewGuid().ToString("N")[..6]);
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    public static bool IsInitialized(string libraryRoot) => File.Exists(new LibraryPaths(libraryRoot).Database);
}
