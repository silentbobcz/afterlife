namespace Afterlife.Core;

/// <summary>Where mods live in the game folder and which folders they share.</summary>
public static class GameLayout
{
    /// <summary>Folders many mods share: a file directly in them belongs to nobody in particular.</summary>
    public static readonly HashSet<string> SharedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "archive", "archive/pc", "archive/pc/mod", "bin", "bin/x64", "bin/x64/plugins", "bin/x64/plugins/cyber_engine_tweaks/mods",
        "r6", "r6/scripts", "r6/tweaks", "r6/config", "r6/config/redsUserHints", "r6/config/settings", "r6/input", "r6/cache", "r6/cache/modded",
        "r6/audioware", "r6/logs", "red4ext", "red4ext/plugins", "red4ext/logs", "mods", "engine", "engine/config", "engine/config/platform",
        "engine/config/platform/pc", "engine/tools",
    };

    /// <summary>
    /// The file sits in a folder of its own mod (a CET mod folder, a RED4ext plugin folder, a redscript module folder),
    /// not directly in a shared folder. Changes there are the mod at work (settings, databases, caches), not a user edit.
    /// </summary>
    public static bool IsInOwnFolder(string path)
    {
        var i = path.LastIndexOf('/');
        return i > 0 && !SharedFolders.Contains(path[..i]);
    }
}
