namespace Afterlife.Core;

public static class Capabilities
{
    public const string Cet = "framework:cet";
    public const string Red4ext = "framework:red4ext";
    public const string Redscript = "framework:redscript";
    public const string ArchiveXl = "framework:archivexl";
    public const string TweakXl = "framework:tweakxl";
    public const string Codeware = "framework:codeware";
    public const string InputLoader = "framework:inputloader";
    public const string ModSettings = "framework:modsettings";
    public const string NativeSettings = "framework:nativesettings";
    public const string RedFileSystem = "framework:redfilesystem";

    public static string DisplayName(string capability) => capability switch
    {
        Cet => "Cyber Engine Tweaks",
        Red4ext => "RED4ext",
        Redscript => "redscript",
        ArchiveXl => "ArchiveXL",
        TweakXl => "TweakXL",
        Codeware => "Codeware",
        InputLoader => "Input Loader",
        ModSettings => "Mod Settings",
        NativeSettings => "Native Settings UI",
        RedFileSystem => "RedFileSystem",
        _ when capability.StartsWith("mod:", StringComparison.Ordinal) => capability[4..],
        _ => capability,
    };
}

/// <summary>
/// Known frameworks: signature files that prove a package (or an unmanaged install) provides a capability,
/// and where they keep runtime files. Since P36 every framework is installed like any other mod; files a framework
/// creates while the game runs are moved aside after it is switched off (P37).
/// </summary>
public sealed record FrameworkContract(
    string Capability,
    IReadOnlyList<string> SignaturePaths,
    bool ContractVerified,
    bool HasActiveRuntimeArtifacts,
    bool ManagedInMvp,
    string Note)
{
    /// <summary>Mod id on nexusmods.com/cyberpunk2077 (P12: link for frameworks that are not installed).</summary>
    public string? NexusModId { get; init; }
    /// <summary>Short label for the core table in the header.</summary>
    public string ShortName { get; init; } = "";
    /// <summary>Optional frameworks are listed as "available" when missing, not as a problem.</summary>
    public bool Optional { get; init; }
    /// <summary>
    /// P37: folders where the framework creates its own files while the game runs (logs, settings, caches).
    /// Files there that AFTERLIFE did not write are moved aside (restorable) after the framework is switched off.
    /// </summary>
    public IReadOnlyList<string> RuntimeFolders { get; init; } = [];
    /// <summary>Sub-folders of <see cref="RuntimeFolders"/> that belong to other mods and are never touched.</summary>
    public IReadOnlyList<string> RuntimeExclude { get; init; } = [];

    public string? NexusUrl => NexusModId is null ? null : $"https://www.nexusmods.com/cyberpunk2077/mods/{NexusModId}";
}

public static class FrameworkCatalog
{
    public static IReadOnlyList<FrameworkContract> Contracts { get; } = new[]
    {
        new FrameworkContract(Capabilities.Cet, new[] { "bin/x64/plugins/cyber_engine_tweaks.asi" }, false, false, true,
            "Signature path to verify in M0.") { NexusModId = "107", ShortName = "CET", RuntimeFolders = ["bin/x64/plugins/cyber_engine_tweaks"], RuntimeExclude = ["bin/x64/plugins/cyber_engine_tweaks/mods"] },
        new FrameworkContract(Capabilities.Red4ext, new[] { "red4ext/RED4ext.dll" }, false, false, true,
            "Signature path to verify in M0.") { NexusModId = "2380", ShortName = "RED4ext", RuntimeFolders = ["red4ext/logs"] },
        new FrameworkContract(Capabilities.Redscript, new[] { "engine/tools/scc.exe" }, false, true, true,
            "Writes r6/cache/final.redscripts(.bk/.modded); disable contract must be verified in M0.") { NexusModId = "1511", ShortName = "redscript" },
        new FrameworkContract(Capabilities.ArchiveXl, new[] { "red4ext/plugins/ArchiveXL/ArchiveXL.dll" }, false, false, true,
            "Signature path to verify in M0.") { NexusModId = "4198", ShortName = "ArchiveXL", RuntimeFolders = ["red4ext/plugins/ArchiveXL"] },
        new FrameworkContract(Capabilities.TweakXl, new[] { "red4ext/plugins/TweakXL/TweakXL.dll" }, false, false, true,
            "Signature path to verify in M0.") { NexusModId = "4197", ShortName = "TweakXL", RuntimeFolders = ["red4ext/plugins/TweakXL"] },
        new FrameworkContract(Capabilities.Codeware, new[] { "red4ext/plugins/Codeware/Codeware.dll" }, false, false, true,
            "Signature path to verify in M0.") { NexusModId = "7780", ShortName = "Codeware", RuntimeFolders = ["red4ext/plugins/Codeware"] },
        new FrameworkContract(Capabilities.InputLoader, new[] { "red4ext/plugins/input_loader/input_loader.dll" }, false, true, true,
            "Creates r6/cache XML and engine/config/platform/pc/input_loader.ini; managed in P2.") { NexusModId = "4575", ShortName = "Input Loader", RuntimeFolders = ["red4ext/plugins/input_loader"] },
        // P12: further common base mods. Folder layouts from their Nexus/GitHub install notes; still to verify in M0.
        new FrameworkContract(Capabilities.ModSettings, new[] { "red4ext/plugins/mod_settings/mod_settings.dll" }, false, true, true,
            "Keeps user choices in red4ext/plugins/mod_settings/user.ini.") { NexusModId = "4885", ShortName = "Mod Settings", Optional = true, RuntimeFolders = ["red4ext/plugins/mod_settings"] },
        new FrameworkContract(Capabilities.NativeSettings, new[] { "bin/x64/plugins/cyber_engine_tweaks/mods/nativeSettings/init.lua" }, false, false, true,
            "CET mod that adds the in-game Mods settings menu.") { NexusModId = "3518", ShortName = "Native Settings", Optional = true },
        new FrameworkContract(Capabilities.RedFileSystem, new[] { "red4ext/plugins/RedFileSystem/RedFileSystem.dll" }, false, false, true,
            "Library for other mods; stores their data in r6/storages.") { NexusModId = "13378", ShortName = "RedFileSystem", Optional = true, RuntimeFolders = ["red4ext/plugins/RedFileSystem"] },
    };

    public static FrameworkContract? Find(string capability) => Contracts.FirstOrDefault(c => c.Capability == capability);

    /// <summary>Capabilities proven by signature files among the given target paths.</summary>
    public static IReadOnlyList<string> DetectProvided(IEnumerable<GamePath> targets)
    {
        var keys = targets.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);
        return Contracts
            .Where(c => c.SignaturePaths.Any(p => keys.Contains(p.ToUpperInvariant())))
            .Select(c => c.Capability)
            .ToList();
    }

    /// <summary>
    /// P40: requirements that cannot be seen from paths, read from script text: Codeware, Mod Settings,
    /// RedFileSystem (redscript imports/annotations) and Native Settings UI (CET Lua).
    /// </summary>
    public static IReadOnlyList<RequirementSpec> InferFromContent(IEnumerable<(GamePath Target, string Text)> scripts, IReadOnlyCollection<string> provided)
    {
        var found = new Dictionary<string, string>();
        foreach (var (t, text) in scripts)
        {
            if (t.Extension == ".reds")
            {
                if (text.Contains("import Codeware", StringComparison.Ordinal)) found.TryAdd(Capabilities.Codeware, "import Codeware in *.reds → Codeware");
                if (text.Contains("@runtimeProperty(\"ModSettings.", StringComparison.Ordinal) || text.Contains("ModSettings.RegisterListenerToClass", StringComparison.Ordinal))
                    found.TryAdd(Capabilities.ModSettings, "ModSettings properties in *.reds → Mod Settings");
                if (text.Contains("import RedFileSystem", StringComparison.Ordinal)) found.TryAdd(Capabilities.RedFileSystem, "import RedFileSystem in *.reds → RedFileSystem");
            }
            else if (t.Extension == ".lua" && text.Contains("GetMod(\"nativeSettings\")", StringComparison.Ordinal))
                found.TryAdd(Capabilities.NativeSettings, "GetMod(\"nativeSettings\") in *.lua → Native Settings UI");
        }
        return found.Where(kv => !provided.Contains(kv.Key))
            .Select(kv => new RequirementSpec(kv.Key, true, "content", kv.Value))
            .OrderBy(r => r.Capability, StringComparer.Ordinal).ToList();
    }

    /// <summary>Catalog capability of a Nexus mod id (P40/P43: requirements listed on the Nexus page).</summary>
    public static string? CapabilityOfNexusId(string? nexusModId) =>
        nexusModId is null ? null : Contracts.FirstOrDefault(c => c.NexusModId == nexusModId)?.Capability;

    /// <summary>Infers requirements from the files a package will actually deploy.</summary>
    public static IReadOnlyList<RequirementSpec> InferRequirements(IEnumerable<GamePath> targets, IReadOnlyCollection<string> provided)
    {
        var found = new Dictionary<string, string>();
        foreach (var t in targets)
        {
            var ext = t.Extension;
            if (ext == ".reds") found.TryAdd(Capabilities.Redscript, "*.reds → redscript");
            if (ext == ".xl") found.TryAdd(Capabilities.ArchiveXl, "*.xl → ArchiveXL");
            if (t.IsUnder("r6/tweaks") && ext is ".yaml" or ".yml" or ".tweak")
                found.TryAdd(Capabilities.TweakXl, "r6/tweaks/*.yaml|*.yml|*.tweak → TweakXL");
            if (t.IsUnder("red4ext/plugins")) found.TryAdd(Capabilities.Red4ext, "red4ext/plugins/* → RED4ext");
            if (t.IsUnder("bin/x64/plugins/cyber_engine_tweaks/mods"))
                found.TryAdd(Capabilities.Cet, "bin/x64/plugins/cyber_engine_tweaks/mods/* → CET");
            if (t.IsUnder("r6/input") && ext == ".xml") found.TryAdd(Capabilities.InputLoader, "r6/input/*.xml → Input Loader");
        }
        return found
            .Where(kv => !provided.Contains(kv.Key))
            .Select(kv => new RequirementSpec(kv.Key, true, "inferred", kv.Value))
            .OrderBy(r => r.Capability, StringComparer.Ordinal)
            .ToList();
    }
}
