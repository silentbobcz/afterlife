using System.Text.Json;
using System.Text.RegularExpressions;
using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>fixer.json in the root of a package, and a removal request (remove-*.json). Written by FIXER.</summary>
public sealed record FixerManifest(int Schema, string? Kind, string? Id, string? Name, string? Version, IReadOnlyList<string>? Requires);

/// <summary>fixer/status.json: tells FIXER whether this library takes its packages.</summary>
public sealed record FixerStatus(int Schema, bool Enabled, string App, string Edition, string Version, DateTimeOffset UpdatedAt);

/// <summary>fixer/outbox/&lt;file&gt;.result.json: what happened to one file from the inbox.</summary>
public sealed record FixerResult(int Schema, string File, string? Id, string Status, string? PackageId, string Message, DateTimeOffset At)
{
    public const string Imported = "imported";
    public const string Applied = "applied";
    public const string Removed = "removed";
    public const string Rejected = "rejected";
}

public sealed class FixerPaths(string libraryRoot)
{
    public string Root { get; } = Path.Combine(Path.GetFullPath(libraryRoot), "fixer");
    public string Inbox => Path.Combine(Root, "inbox");
    public string Outbox => Path.Combine(Root, "outbox");
    public string Done => Path.Combine(Root, "done");
    public string Status => Path.Combine(Root, "status.json");
}

/// <summary>
/// The drop box for FIXER, the quest editor: FIXER leaves a zip (game folder layout plus fixer.json) or a removal
/// request in fixer/inbox of the library; AFTERLIFE imports it like any other mod, puts it into the category FIXER,
/// switches it on in the selected profile and answers in fixer/outbox. The game changes only on APPLY, as always.
/// Off by default; while it is off the inbox is never read. The contract (schema 1) is shared with FIXER.
/// </summary>
public sealed class FixerService
{
    public const int Schema = 1;
    public const string CategoryName = "FIXER";
    /// <summary><see cref="RequirementSpec.Origin"/> of a requirement FIXER listed itself.</summary>
    public const string RequirementOrigin = "fixer";

    private const string EnabledKey = "fixer.enabled";
    private const string PackageKey = "fixer.package:"; // + quest id → its package in the library
    private const string LastKey = "fixer.last:";       // + quest id → the last result written for it
    private const int KeptFiles = 20;

    private readonly InstallContext _ctx;
    // files that could not be moved out of the inbox are not taken a second time (name, size, time of the last write)
    private readonly HashSet<string> _handled = new(StringComparer.Ordinal);

    private AfterlifeStore Store => _ctx.Store;
    public FixerPaths Paths { get; }

    internal FixerService(InstallContext ctx)
    {
        _ctx = ctx;
        Paths = new FixerPaths(ctx.LibraryPaths.Root);
    }

    public bool Enabled => Store.GetMeta(EnabledKey) == "1";

    public void SetEnabled(bool on, string edition, string version)
    {
        Store.SetMeta(EnabledKey, on ? "1" : "0");
        Announce(edition, version);
    }

    /// <summary>
    /// Writes status.json. A library where the drop box was never switched on gets no fixer folder at all;
    /// FIXER reads a missing status the same way as a disabled one.
    /// </summary>
    public void Announce(string edition, string version)
    {
        var on = Enabled;
        if (!on && !Directory.Exists(Paths.Root)) return;
        if (on) foreach (var d in new[] { Paths.Inbox, Paths.Outbox, Paths.Done }) Directory.CreateDirectory(d);
        WriteJson(Paths.Status, new FixerStatus(Schema, on, "AFTERLIFE", edition, version, DateTimeOffset.UtcNow));
    }

    /// <summary>Something waits in the inbox. Touches only the disk, so the window may ask from its own thread.</summary>
    public static bool HasPending(string libraryRoot)
    {
        try
        {
            var inbox = new FixerPaths(libraryRoot).Inbox;
            return Directory.Exists(inbox) && Directory.EnumerateFiles(inbox).Any(IsRequest);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // FIXER writes "<name>.zip.part" and renames it when the file is complete: a .part is never a request
    private static bool IsPackage(string path) => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    private static bool IsRemoval(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("remove-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRequest(string path) => IsPackage(path) || IsRemoval(path);

    /// <summary>
    /// Takes everything that waits in the inbox, oldest first, and returns what happened to each file.
    /// Does nothing while the drop box is off, while another AFTERLIFE window changes this install
    /// or while an interrupted change waits for recovery.
    /// </summary>
    public IReadOnlyList<FixerResult> Process()
    {
        if (!Enabled || !Directory.Exists(Paths.Inbox)) return [];
        var files = Directory.EnumerateFiles(Paths.Inbox).Where(IsRequest).Select(f => new FileInfo(f))
            .OrderBy(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal).ToList();
        if (files.Count == 0) return [];

        InstallLock gate;
        try { gate = InstallLock.Acquire(_ctx.LibraryPaths, _ctx.GameRoot); }
        catch (InstallBusyException) { return []; }
        using (gate)
        {
            if (Store.GetUnfinishedOperation() is not null) return [];
            var results = new List<FixerResult>();
            foreach (var f in files)
            {
                var key = $"{f.Name}|{f.Length}|{f.LastWriteTimeUtc.Ticks}";
                if (_handled.Contains(key) || !IsFree(f.FullName)) continue;
                FixerResult r;
                try { r = IsPackage(f.Name) ? TakePackage(f) : TakeRemoval(f); }
                catch (Exception ex) when (ex is ImportException or JsonException or InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    r = Result(f.Name, null, FixerResult.Rejected, null, ex.Message);
                }
                WriteResult(r);
                PutAway(f);
                _handled.Add(key);
                ActivityLog.Write("fixer", $"{f.Name} → {r.Status}: {r.Message}");
                results.Add(r);
            }
            if (results.Count > 0)
            {
                Sweep();
                Prune(Paths.Done);
                Prune(Paths.Outbox);
            }
            return results;
        }
    }

    /// <summary>
    /// After a change of the game: quests that are in the game now are reported as applied, and older builds
    /// the game has let go leave the library.
    /// </summary>
    public void AfterApply()
    {
        if (!Enabled) return;
        InstallLock gate;
        try { gate = InstallLock.Acquire(_ctx.LibraryPaths, _ctx.GameRoot); }
        catch (InstallBusyException) { return; }
        using (gate)
        {
            if (Store.GetUnfinishedOperation() is null) Sweep();
        }
    }

    private FixerResult TakePackage(FileInfo f)
    {
        var raw = PackageLibrary.ReadEntry(f.FullName, "fixer.json") ?? throw new ImportException("no fixer.json in the root of the zip");
        var m = ReadManifest(raw);
        if (m.Kind is not ("quest" or "tool")) throw new ImportException($"fixer.json: kind must be quest or tool, not '{m.Kind}'");
        var id = m.Id!;
        var name = string.IsNullOrWhiteSpace(m.Name) ? id : m.Name.Trim();
        if (name.Length > 80) name = name[..80];
        var version = string.IsNullOrWhiteSpace(m.Version) ? f.LastWriteTimeUtc.ToString("yyyy-MM-ddTHHmm") : m.Version.Trim();

        var import = _ctx.Library.Import(new ImportRequest(f.FullName, name, version, ModIdOf(id), CategoryName));
        if (import.Package is not { } p)
            return Result(f.Name, id, FixerResult.Rejected, null, string.Join("; ", import.Analysis.Problems));

        var known = (m.Requires ?? []).Where(c => FrameworkCatalog.Find(c) is not null).Distinct(StringComparer.Ordinal).ToList();
        var unknown = (m.Requires ?? []).Except(known, StringComparer.Ordinal).ToList();
        var extra = known.Where(c => !p.Provides.Contains(c) && p.Requirements.All(r => r.Capability != c))
            .Select(c => new RequirementSpec(c, true, RequirementOrigin, $"listed by FIXER → {Capabilities.DisplayName(c)}")).ToList();
        if (extra.Count > 0)
        {
            p = p with { Requirements = p.Requirements.Concat(extra).OrderBy(r => r.Capability, StringComparer.Ordinal).ToList() };
            Store.UpdatePackage(p);
        }

        // a new build takes the place of the previous one: same profiles, same on/off, same category
        var oldId = Store.GetMeta(PackageKey + id);
        var old = oldId is null || oldId == p.PackageId ? null : Store.GetPackage(oldId);
        if (old is not null) _ctx.Library.ReplaceVersion(old.PackageId, p.PackageId);
        Store.SetMeta(PackageKey + id, p.PackageId);
        PutIntoCategory(p.PackageId, old?.PackageId);

        var selected = _ctx.SelectedProfile;
        string message;
        if (selected.IsSystemModsOff)
            message = $"{name} is in the library. Select a profile, switch it on and press APPLY CHANGES.";
        else
        {
            if (!selected.IsEnabled(p.PackageId)) _ctx.Profiles.SetEnabledMany(selected.Id, [p.PackageId], true);
            message = $"{name} is in the library and switched on in {selected.Name}. Press APPLY CHANGES.";
        }
        if (unknown.Count > 0) message += $" Unknown requirement(s) ignored: {string.Join(", ", unknown)}.";
        return Result(f.Name, id, FixerResult.Imported, p.PackageId, message);
    }

    private FixerResult TakeRemoval(FileInfo f)
    {
        var m = ReadManifest(File.ReadAllBytes(f.FullName));
        if (m.Kind != "remove") throw new ImportException($"kind must be remove, not '{m.Kind}'");
        var id = m.Id!;
        var packageId = Store.GetMeta(PackageKey + id);
        var p = packageId is null ? null : Store.GetPackage(packageId);
        if (p is null) return Result(f.Name, id, FixerResult.Removed, null, $"{id} is not in the library; nothing to switch off.");
        _ctx.Library.DeactivateEverywhere([p.PackageId]);
        var inGame = Store.GetLedger().Values.Any(l => l.Owner == p.PackageId);
        return Result(f.Name, id, FixerResult.Removed, p.PackageId, inGame
            ? $"{p.ModName} is switched off in every profile. Press APPLY CHANGES to take it out of the game."
            : $"{p.ModName} is switched off in every profile.");
    }

    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);

    private static FixerManifest ReadManifest(byte[] raw)
    {
        ReadOnlySpan<byte> json = raw;
        if (json.Length >= 3 && json[0] == 0xEF && json[1] == 0xBB && json[2] == 0xBF) json = json[3..];
        var m = JsonSerializer.Deserialize<FixerManifest>(json, Json.Options) ?? throw new ImportException("the request is empty");
        if (m.Schema != Schema) throw new ImportException($"schema {m.Schema} is not supported; this AFTERLIFE reads schema {Schema}");
        if (m.Id is null || !IdPattern.IsMatch(m.Id))
            throw new ImportException("id must be 1 to 64 characters: lower-case letters, digits, _ and -");
        return m;
    }

    private static string ModIdOf(string id) => "fixer-" + id;

    private static FixerResult Result(string file, string? id, string status, string? packageId, string message) =>
        new(Schema, file, id, status, packageId, message, DateTimeOffset.UtcNow);

    /// <summary>The category of the build it replaces, otherwise FIXER (made when it is not there yet).</summary>
    private void PutIntoCategory(string packageId, string? replacedId)
    {
        var layout = BlockLayout.FromJson(Store.GetMeta(BlockLayout.StoreKey));
        var category = (replacedId is null ? null : layout.CategoryIdOf(replacedId)) ?? layout.CategoryIdOf(packageId);
        if (category is null)
        {
            var own = layout.Categories.FirstOrDefault(c => string.Equals(c.Name, CategoryName, StringComparison.OrdinalIgnoreCase));
            if (own is null)
            {
                own = new ModCategory(BlockLayout.NewCategoryId(), CategoryName);
                layout = layout.WithCategories([.. layout.Categories, own]);
            }
            category = own.Id;
        }
        Store.SetMeta(BlockLayout.StoreKey, layout.Assign([packageId], category).ToJson());
    }

    /// <summary>Older builds no profile uses any more are deleted; results of quests that reached the game say so.</summary>
    private void Sweep()
    {
        var last = Store.GetLastGeneration();
        var inGame = new HashSet<string>(StringComparer.Ordinal);
        if (last is not null) inGame.UnionWith(Json.Deserialize<DeploymentSnapshot>(last.SnapshotJson).IncludedPackages);
        foreach (var (key, current) in Store.GetMetaByPrefix(PackageKey))
        {
            var id = key[PackageKey.Length..];
            var stale = Store.GetPackages()
                .Where(x => x.ModId == ModIdOf(id) && x.PackageId != current && x.LayerOfModId is null).Select(x => x.PackageId).ToList();
            foreach (var packageId in stale)
            {
                // still in the game or wanted by a profile: it goes after the next apply
                try { _ctx.Library.RemoveFromLibrary(packageId); }
                catch (InvalidOperationException) { }
            }
            if (Store.GetMeta(LastKey + id) is not { } json) continue;
            FixerResult r;
            try { r = Json.Deserialize<FixerResult>(json); }
            catch (JsonException) { continue; }
            if (r.Status != FixerResult.Imported || r.PackageId is null || !inGame.Contains(r.PackageId)) continue;
            var name = Store.GetPackage(r.PackageId)?.ModName ?? id;
            WriteResult(r with { Status = FixerResult.Applied, Message = $"{name} is in the game.", At = DateTimeOffset.UtcNow });
        }
    }

    private void WriteResult(FixerResult r)
    {
        try
        {
            Directory.CreateDirectory(Paths.Outbox);
            WriteJson(Path.Combine(Paths.Outbox, r.File + ".result.json"), r);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ActivityLog.Write("fixer", $"result for {r.File} could not be written: {ex.Message}");
        }
        if (r.Id is not null && r.Status != FixerResult.Rejected) Store.SetMeta(LastKey + r.Id, Json.Serialize(r));
    }

    /// <summary>The file leaves the inbox: into done, or deleted when that fails.</summary>
    private void PutAway(FileInfo f)
    {
        try
        {
            Directory.CreateDirectory(Paths.Done);
            File.Move(f.FullName, Path.Combine(Paths.Done, f.Name), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(f.FullName); }
            catch (Exception ex2) when (ex2 is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Atomic for the reader: the file appears under its name only when it is complete.</summary>
    private static void WriteJson<T>(string path, T value)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json.Options));
        File.Move(tmp, path, overwrite: true);
    }

    private static bool IsFree(string path)
    {
        try
        {
            using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>done and outbox keep the newest files only: builds with voice are large and arrive often.</summary>
    private static void Prune(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in new DirectoryInfo(dir).GetFiles().OrderByDescending(f => f.LastWriteTimeUtc).Skip(KeptFiles)) f.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
