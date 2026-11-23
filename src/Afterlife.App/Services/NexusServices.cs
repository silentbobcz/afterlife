using System.IO;
using System.Security.Cryptography;
using System.Text;
using Afterlife.Infrastructure;
using Microsoft.Win32;

namespace Afterlife.App.Services;

/// <summary>
/// The user's personal Nexus Mods API key, encrypted with Windows DPAPI for the current Windows user.
/// Stored next to settings.json; never written to logs or the library.
/// </summary>
public static class NexusKeyStore
{
    private static string FilePath => Path.Combine(AppSettings.SettingsDirectory, "nexus.key");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AFTERLIFE nexus api key v1");

    public static string? Load()
    {
        if (AppEdition.Offline) return null; // a key saved by the full edition stays where it is, unused
        try
        {
            if (!File.Exists(FilePath)) return null;
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
            var key = Encoding.UTF8.GetString(plain).Trim();
            return key.Length == 0 ? null : key;
        }
        catch (CryptographicException) { return null; } // copied from another Windows account: ask again
        catch (IOException) { return null; }
    }

    public static void Save(string key)
    {
        if (AppEdition.Offline) throw new InvalidOperationException("AFTERLIFE Offline does not use a Nexus Mods API key");
        Directory.CreateDirectory(AppSettings.SettingsDirectory);
        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), Entropy, DataProtectionScope.CurrentUser);
        var tmp = FilePath + ".tmp";
        File.WriteAllBytes(tmp, data);
        File.Move(tmp, FilePath, overwrite: true);
    }

    public static void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch (IOException) { }
    }
}

/// <summary>
/// Registers AFTERLIFE for nxm:// links (Mod Manager Download) for the current Windows user only (HKCU).
/// The previous handler (Vortex, MO2, …) is remembered and put back when AFTERLIFE lets go.
/// </summary>
public static class NxmProtocol
{
    private const string KeyPath = @"Software\Classes\nxm";
    private const string CommandPath = KeyPath + @"\shell\open\command";
    private static string BackupFile => Path.Combine(AppSettings.SettingsDirectory, "nxm-previous-handler.txt");

    public static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "AFTERLIFE.exe");
    private static string OurCommand => $"\"{ExePath}\" \"%1\"";

    public static string? CurrentCommand()
    {
        using var k = Registry.CurrentUser.OpenSubKey(CommandPath);
        return k?.GetValue(null) as string;
    }

    public static bool IsRegistered => string.Equals(CurrentCommand(), OurCommand, StringComparison.OrdinalIgnoreCase);

    /// <summary>Another program that handles nxm links now, if any (shown before taking over).</summary>
    public static string? OtherHandler()
    {
        var cmd = CurrentCommand();
        if (cmd is null || IsRegistered) return null;
        var exe = cmd.Trim().StartsWith('"') ? cmd.Trim().Split('"', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() : cmd.Split(' ')[0];
        return string.IsNullOrWhiteSpace(exe) ? cmd : Path.GetFileName(exe);
    }

    public static void Register()
    {
        if (AppEdition.Offline) return; // Mod Manager Download needs the API
        var previous = CurrentCommand();
        if (previous is not null && !IsRegistered)
        {
            Directory.CreateDirectory(AppSettings.SettingsDirectory);
            File.WriteAllText(BackupFile, previous);
        }
        using var root = Registry.CurrentUser.CreateSubKey(KeyPath);
        root.SetValue(null, "URL:NXM Protocol");
        root.SetValue("URL Protocol", "");
        using var cmd = Registry.CurrentUser.CreateSubKey(CommandPath);
        cmd.SetValue(null, OurCommand);
    }

    public static void Unregister()
    {
        if (!IsRegistered) return;
        var previous = File.Exists(BackupFile) ? File.ReadAllText(BackupFile).Trim() : null;
        if (!string.IsNullOrWhiteSpace(previous))
        {
            using var cmd = Registry.CurrentUser.CreateSubKey(CommandPath);
            cmd.SetValue(null, previous);
        }
        else Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false);
        try { File.Delete(BackupFile); } catch (IOException) { }
    }
}
