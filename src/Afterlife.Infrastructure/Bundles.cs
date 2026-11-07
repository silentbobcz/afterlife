using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>Reading collection bundles and FOMOD installers out of archives.</summary>
public sealed class BundleService
{
    private readonly InstallContext _ctx;
    private AfterlifeStore Store => _ctx.Store;
    private LibraryPaths Library => _ctx.LibraryPaths;

    internal BundleService(InstallContext ctx) => _ctx = ctx;

    public IReadOnlyList<CollectionRecord> Collections() => CollectionStore.List(Store);

    /// <summary>Reads collection.json from a downloaded collection bundle and merges it with the API revision.</summary>
    public CollectionRevisionInfo ReadCollectionBundle(CollectionRevisionInfo api, string bundleFile)
    {
        var entries = PackageLibrary.ListEntries(bundleFile);
        var manifest = entries.FirstOrDefault(e => e.Equals("collection.json", StringComparison.OrdinalIgnoreCase))
            ?? entries.Where(e => e.EndsWith("/collection.json", StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.Length).FirstOrDefault();
        if (manifest is null) return api;
        var bytes = PackageLibrary.ReadEntry(bundleFile, manifest);
        if (bytes is null) return api;
        try { return CollectionLogic.FromManifest(api, System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF')); }
        catch (System.Text.Json.JsonException) { return api; }
    }

    /// <summary>A load order the collection ships as archive/pc/mod/modlist.txt inside one of its mods (archive names, first wins).</summary>
    public static IReadOnlyList<string> ModlistInArchive(string archiveFile)
    {
        try
        {
            var path = PackageLibrary.ListEntries(archiveFile).FirstOrDefault(e => e.EndsWith("archive/pc/mod/modlist.txt", StringComparison.OrdinalIgnoreCase));
            if (path is null) return [];
            var bytes = PackageLibrary.ReadEntry(archiveFile, path);
            return bytes is null ? [] : System.Text.Encoding.UTF8.GetString(bytes).Split('\n')
                .Select(l => l.Trim().TrimStart('\uFEFF')).Where(l => l.Length > 0 && !l.StartsWith('#') && l.EndsWith(".archive", StringComparison.OrdinalIgnoreCase)).ToList();
        }
        catch (ImportException) { return []; }
    }

    /// <summary>A mod that came inside the collection bundle, as a file to import (null when not found).</summary>
    public string? ExtractBundled(string bundleFile, CollectionItem item)
    {
        var name = item.LogicalFileName ?? item.Name;
        var entries = PackageLibrary.ListEntries(bundleFile);
        Directory.CreateDirectory(Library.Downloads);
        var file = entries.FirstOrDefault(e => e.StartsWith("bundled/", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(e).Equals(name, StringComparison.OrdinalIgnoreCase) && Path.GetExtension(e).ToLowerInvariant() is ".zip" or ".7z" or ".rar");
        if (file is not null)
        {
            var bytes = PackageLibrary.ReadEntry(bundleFile, file);
            if (bytes is null) return null;
            var dest = Path.Combine(Library.Downloads, NexusClient.SafeFileName(Path.GetFileName(file)));
            File.WriteAllBytes(dest, bytes);
            return dest;
        }
        var folder = "bundled/" + name;
        if (!entries.Any(e => e.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))) return null;
        var zip = Path.Combine(Library.Downloads, NexusClient.SafeFileName(name) + ".zip");
        if (File.Exists(zip)) File.Delete(zip);
        return PackageLibrary.RepackFolder(bundleFile, folder, zip) > 0 ? zip : null;
    }

    public FomodConfig ReadFomod(string archiveFile, string configPath)
    {
        var bytes = PackageLibrary.ReadEntry(archiveFile, configPath) ?? throw new ImportException("the installer description is missing in the archive");
        var text = DecodeXml(bytes);
        try { return Fomod.Parse(text); }
        catch (System.Xml.XmlException ex) { throw new ImportException("the installer description cannot be read: " + ex.Message); }
    }

    /// <summary>FOMOD texts are often UTF-16 with a BOM; otherwise UTF-8.</summary>
    private static string DecodeXml(byte[] b)
    {
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return System.Text.Encoding.Unicode.GetString(b, 2, b.Length - 2);
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return System.Text.Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return System.Text.Encoding.UTF8.GetString(b, 3, b.Length - 3);
        if (b.Length >= 2 && b[1] == 0) return System.Text.Encoding.Unicode.GetString(b);
        return System.Text.Encoding.UTF8.GetString(b);
    }

    public static byte[]? ReadFomodImage(string archiveFile, string configPath, string imagePath)
    {
        var root = Fomod.RootOf(configPath);
        var path = (root.Length == 0 ? "" : root + "/") + imagePath.Replace('\\', '/');
        try { return PackageLibrary.ReadEntry(archiveFile, path); }
        catch (ImportException) { return null; }
    }
}
