using System.Security.Cryptography;
using Afterlife.Core;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Afterlife.Infrastructure;

public sealed class LibraryPaths(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    public string Database => Path.Combine(Root, "afterlife.db");
    public string Packages => Path.Combine(Root, "packages");
    public string Staging => Path.Combine(Root, "staging");
    public string Downloads => Path.Combine(Root, "downloads");
    public string Locks => Path.Combine(Root, "locks");
    public string Backups => Path.Combine(Root, "backups");
    /// <summary>P86: pictures of mods from their Nexus pages, one small file per mod.</summary>
    public string Images => Path.Combine(Root, "cache", "images");

    public string PackageRoot(string packageId) => Path.Combine(Packages, packageId);
    public string PackageFile(string packageId, string storedPath) =>
        Path.Combine(PackageRoot(packageId), "files", storedPath.Replace('/', Path.DirectorySeparatorChar));

    public void EnsureCreated()
    {
        foreach (var d in new[] { Root, Packages, Staging, Downloads, Locks, Backups }) Directory.CreateDirectory(d);
    }
}

public sealed record ImportRequest(
    string ArchivePath,
    string? ModName = null,
    string? Version = null,
    string? ExistingModId = null,
    string? Category = null,
    ImportOptions? Options = null,
    string? NexusModId = null,
    string? NexusFileId = null)
{
    /// <summary>R3: stored with the package.</summary>
    public IReadOnlyList<FomodChoice>? FomodChoices { get; init; }
}

public sealed record ImportResult(ImportAnalysis Analysis, PackageManifest? Package)
{
    public bool Succeeded => Package is not null;
}

public sealed class ImportException(string message) : Exception(message);

/// <summary>Imports archives into immutable library packages. Never writes into the game folder.</summary>
public sealed class PackageLibrary(LibraryPaths paths, AfterlifeStore store)
{
    private static readonly string[] ArchiveExtensions = { ".zip", ".7z", ".rar" };

    public ImportResult Import(ImportRequest req, ImportLimits? limits = null)
    {
        limits ??= ImportLimits.Default;
        var file = Path.GetFullPath(req.ArchivePath);
        if (!File.Exists(file)) throw new ImportException($"file not found: {file}");
        if (IsInside(file, paths.Root)) { /* importing from downloads folder is fine */ }
        var ext = Path.GetExtension(file).ToLowerInvariant();

        if (ext is ".archive" or ".xl")
        {
            var analysis = ImportAnalyzer.Analyze([new ArchiveEntryInfo(Path.GetFileName(file), new FileInfo(file).Length)], req.Options, limits);
            if (analysis.Status != ImportStatus.Ok) return new ImportResult(analysis, null);
            return Store(req, file, analysis, staging =>
            {
                var m = analysis.Mapped.Single();
                using var src = File.OpenRead(file);
                return new[] { WriteEntry(staging, m, src, limits.MaxTotalBytes) };
            });
        }

        if (!ArchiveExtensions.Contains(ext)) throw new ImportException($"unsupported file type '{ext}'; use .zip, .7z, .rar or .archive");

        using var archive = OpenArchive(file);
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        if (entries.Any(e => e.IsEncrypted)) throw new ImportException("encrypted archives are not supported");
        var infos = entries.Select(e => new ArchiveEntryInfo(e.Key ?? "", e.Size)).ToList();
        var result = ImportAnalyzer.Analyze(infos, req.Options, limits);
        if (result.Status != ImportStatus.Ok) return new ImportResult(result, null);

        var wanted = result.Mapped.ToDictionary(m => m.RawPath, StringComparer.Ordinal);
        return Store(req, file, result, staging =>
        {
            var written = new List<PackageFile>();
            long total = 0;
            if (archive.IsSolid || archive.Type == SharpCompress.Common.ArchiveType.SevenZip)
            {
                // Solid archives must be read sequentially.
                using var reader = archive.ExtractAllEntries();
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.IsDirectory || reader.Entry.Key is null) continue;
                    if (!wanted.TryGetValue(reader.Entry.Key, out var m)) continue;
                    using var es = reader.OpenEntryStream();
                    var pf = WriteEntry(staging, m, es, limits.MaxTotalBytes - total);
                    total += pf.Size;
                    written.Add(pf);
                }
            }
            else
            {
                foreach (var e in entries)
                {
                    if (e.Key is null || !wanted.TryGetValue(e.Key, out var m)) continue;
                    using var es = e.OpenEntryStream();
                    var pf = WriteEntry(staging, m, es, limits.MaxTotalBytes - total);
                    total += pf.Size;
                    written.Add(pf);
                }
            }
            if (written.Count != wanted.Count) throw new ImportException("archive could not be read completely");
            return written;
        });
    }

    /// <summary>Paths of all files in an archive.</summary>
    public static IReadOnlyList<string> ListEntries(string archiveFile)
    {
        using var archive = OpenArchive(Path.GetFullPath(archiveFile));
        return archive.Entries.Where(e => !e.IsDirectory && e.Key is not null).Select(e => e.Key!.Replace('\\', '/')).ToList();
    }

    /// <summary>P51: copies the entries under a folder of an archive into a new zip (bundled mods of a collection).</summary>
    public static int RepackFolder(string archiveFile, string folder, string zipDestination)
    {
        var prefix = folder.TrimEnd('/') + "/";
        using var archive = OpenArchive(Path.GetFullPath(archiveFile));
        Directory.CreateDirectory(Path.GetDirectoryName(zipDestination)!);
        using var zip = System.IO.Compression.ZipFile.Open(zipDestination, System.IO.Compression.ZipArchiveMode.Create);
        var n = 0;
        using var reader = archive.ExtractAllEntries();
        while (reader.MoveToNextEntry())
        {
            var key = reader.Entry.Key?.Replace('\\', '/');
            if (reader.Entry.IsDirectory || key is null || !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var e = zip.CreateEntry(key[prefix.Length..]);
            using var dst = e.Open();
            using var src = reader.OpenEntryStream();
            src.CopyTo(dst);
            n++;
        }
        return n;
    }

    /// <summary>R3: one file from an archive (FOMOD config and its pictures), at most 16 MB.</summary>
    public static byte[]? ReadEntry(string archiveFile, string rawPath)
    {
        using var archive = OpenArchive(Path.GetFullPath(archiveFile));
        var wanted = rawPath.Replace('\\', '/');
        bool Match(string? k) => k is not null && string.Equals(k.Replace('\\', '/'), wanted, StringComparison.OrdinalIgnoreCase);
        if (archive.IsSolid || archive.Type == SharpCompress.Common.ArchiveType.SevenZip)
        {
            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
                if (!reader.Entry.IsDirectory && Match(reader.Entry.Key)) return ReadLimited(reader.OpenEntryStream());
            return null;
        }
        var e = archive.Entries.FirstOrDefault(x => !x.IsDirectory && Match(x.Key));
        return e is null ? null : ReadLimited(e.OpenEntryStream());
    }

    private static byte[] ReadLimited(Stream s)
    {
        using (s)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = s.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
                if (ms.Length > 16 * 1024 * 1024) throw new ImportException("file in the archive is too large");
            }
            return ms.ToArray();
        }
    }

    private static IArchive OpenArchive(string file)
    {
        try
        {
            var a = ArchiveFactory.OpenArchive(file);
            if (!a.IsComplete) { a.Dispose(); throw new ImportException("multi-part archives are not supported"); }
            return a;
        }
        catch (ImportException) { throw; }
        catch (Exception ex) { throw new ImportException($"cannot read archive: {ex.Message}"); }
    }

    private static PackageFile WriteEntry(string stagingFiles, MappedEntry m, Stream source, long budget)
    {
        var dest = Path.Combine(stagingFiles, m.Target.Value.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var hasher = new System.IO.Hashing.XxHash128();
        long size = 0;
        var buffer = new byte[1 << 20];
        using (var dst = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                size += read;
                if (size > budget) throw new ImportException("archive expands beyond the size limit");
                hasher.Append(buffer.AsSpan(0, read));
                dst.Write(buffer, 0, read);
            }
            dst.Flush(flushToDisk: true);
        }
        File.SetAttributes(dest, File.GetAttributes(dest) | FileAttributes.ReadOnly);
        return new PackageFile(m.Target.Value, m.Target.Value, Convert.ToHexStringLower(hasher.GetHashAndReset()), size);
    }

    private ImportResult Store(ImportRequest req, string sourceFile, ImportAnalysis analysis, Func<string, IReadOnlyList<PackageFile>> extract)
    {
        paths.EnsureCreated();
        var packageId = "pkg-" + Guid.NewGuid().ToString("N")[..12];
        var staging = Path.Combine(paths.Staging, packageId);
        var stagingFiles = Path.Combine(staging, "files");
        Directory.CreateDirectory(stagingFiles);
        try
        {
            var files = extract(stagingFiles).OrderBy(f => f.Target.ToUpperInvariant(), StringComparer.Ordinal).ToList();
            string md5;
            using (var s = File.OpenRead(sourceFile)) md5 = Convert.ToHexStringLower(MD5.HashData(s));
            var guess = GuessFromFileName(sourceFile);
            var name = req.ModName ?? guess.Name;
            var modId = req.ExistingModId ?? "mod-" + Slug(name) + "-" + packageId[4..10];
            var manifest = new PackageManifest(
                packageId, modId, name, req.Version ?? guess.Version, req.NexusModId is null ? PackageOrigin.Manual : PackageOrigin.Nexus,
                null, ContentHash.ManifestDigest(files), files, analysis.Provides, analysis.Requirements, req.Category,
                req.NexusModId ?? guess.NexusModId, req.NexusFileId, Path.GetFileName(sourceFile), md5, DateTimeOffset.UtcNow)
            { FomodChoices = req.FomodChoices };
            Directory.Move(staging, paths.PackageRoot(packageId));
            store.AddPackage(manifest);
            return new ImportResult(analysis, manifest);
        }
        catch
        {
            TryDeleteTree(staging);
            throw;
        }
    }

    /// <summary>Stores the current content of changed managed files as a local "user changes" layer of a mod.</summary>
    public PackageManifest CaptureLayer(string gameRoot, string basePackageId, IEnumerable<string> gamePaths, IFileOps fs)
    {
        var basePkg = store.GetPackage(basePackageId) ?? throw new ImportException($"unknown package {basePackageId}");
        paths.EnsureCreated();
        var packageId = "pkg-" + Guid.NewGuid().ToString("N")[..12];
        var staging = Path.Combine(paths.Staging, packageId);
        var stagingFiles = Path.Combine(staging, "files");
        Directory.CreateDirectory(stagingFiles);
        try
        {
            var files = new List<PackageFile>();
            foreach (var raw in gamePaths)
            {
                var gp = GamePath.Parse(raw);
                var src = gp.ToOsPath(gameRoot);
                fs.EnsureNoReparsePoints(gameRoot, src);
                using var s = File.OpenRead(src);
                files.Add(WriteEntry(stagingFiles, new MappedEntry(gp.Value, gp, s.Length), s, long.MaxValue));
            }
            var manifest = new PackageManifest(packageId, basePkg.ModId, basePkg.ModName + " (my changes)", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                PackageOrigin.Captured, basePkg.ModId, ContentHash.ManifestDigest(files), files, [], [], basePkg.Category, ImportedAt: DateTimeOffset.UtcNow);
            Directory.Move(staging, paths.PackageRoot(packageId));
            store.AddPackage(manifest);
            return manifest;
        }
        catch
        {
            TryDeleteTree(staging);
            throw;
        }
    }

    /// <summary>
    /// Deletes a package unless a profile, the deployed generation or an unfinished operation needs it.
    /// Older generations do not protect it any more (P22): recovery only ever returns to the last generation.
    /// </summary>
    public void Delete(string packageId)
    {
        var referencedBy = store.GetProfiles().Where(p => p.Entries.Any(e => e.PackageId == packageId)).Select(p => p.Name).ToList();
        if (referencedBy.Count > 0) throw new InvalidOperationException($"package is used by profile(s): {string.Join(", ", referencedBy)}");
        if (store.GetPackagesInRecentGenerations(1).Contains(packageId)) throw new InvalidOperationException("still in the game; apply first, then delete");
        var unfinished = store.GetUnfinishedOperation();
        if (unfinished is not null && Json.Deserialize<DeploymentSnapshot>(unfinished.TargetSnapshotJson).IncludedPackages.Contains(packageId))
            throw new InvalidOperationException("package is needed by an unfinished operation");
        if (store.GetLedger().Values.Any(l => l.Owner == packageId)) throw new InvalidOperationException("package files are still deployed");
        store.DeletePackageRecord(packageId);
        TryDeleteTree(paths.PackageRoot(packageId));
    }

    private static void TryDeleteTree(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(dir, true);
    }

    private static bool IsInside(string file, string dir) =>
        Path.GetFullPath(file).StartsWith(Path.GetFullPath(dir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Slug(string s)
    {
        var chars = s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Length > 24 ? slug[..24].Trim('-') : (slug.Length == 0 ? "mod" : slug);
    }

    /// <summary>
    /// Best-effort name/version from the archive file name. Nexus downloads look like
    /// "Mod Name-1234-1-2-3-1700000000.zip" (name, mod id, version, upload time).
    /// </summary>
    public static (string Name, string Version, string? NexusModId) GuessFromFileName(string file)
    {
        var stem = Path.GetFileNameWithoutExtension(file);
        var nexus = System.Text.RegularExpressions.Regex.Match(stem, @"^(?<name>.+?)-(?<id>\d{1,7})-(?<ver>\d+(?:-\d+){0,4}[a-z]?)-(?<ts>\d{9,11})$");
        if (nexus.Success)
            return (nexus.Groups["name"].Value.Trim(), nexus.Groups["ver"].Value.Replace('-', '.'), nexus.Groups["id"].Value);
        // P63: newer Nexus names "Name-4885-0.2.27-2026-10-26T22-14Z-cB3xB00IB" (separators may also be spaces)
        var dated = System.Text.RegularExpressions.Regex.Match(stem,
            @"^(?<name>.+?)[-\s]+(?<id>\d{1,7})[-\s]+v?(?<ver>\d+(?:[.\-]\d+){0,4}[a-z]?)[-\s]+(?<ts>\d{4}-\d{2}-\d{2}T\d{2}[-:]?\d{2}(?:[-:]?\d{2})?Z?)(?:[-\s]+[A-Za-z0-9]{1,16})?$");
        if (dated.Success)
            return (dated.Groups["name"].Value.Trim(), dated.Groups["ver"].Value.Replace('-', '.'), dated.Groups["id"].Value);
        var plain = System.Text.RegularExpressions.Regex.Match(stem, @"^(?<name>.*?)[\s_\-]*v?(?<ver>\d+(?:[.\-_]\d+){1,3}[a-z]?)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (plain.Success && plain.Groups["name"].Value.Trim().Length > 0)
            return (plain.Groups["name"].Value.Trim(), plain.Groups["ver"].Value.Replace('-', '.').Replace('_', '.'), null);
        return (stem, "unknown", null);
    }
}
