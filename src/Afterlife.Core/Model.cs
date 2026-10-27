using System.IO.Hashing;
using System.Text;

namespace Afterlife.Core;

public enum Severity { Info, Warning, Blocking }

public enum IssueKind
{
    MissingRequirement,
    EnabledWithOverride,
    FileConflict,
    ForeignFileAtTarget,
    ModifiedManagedFile,
    ModlistChangedExternally,
    ModlistForeignNeedsTakeover,
    UnmanagedArchivesInCustomOrder,
    FrameworkContractUnverified,
    ArchiveContentsNotAnalyzed,
    BuildChanged,
    GameRunning,
    RecoveryRequired,
    MissingPackage,
}

public sealed record Issue(
    IssueKind Kind,
    Severity Severity,
    string Message,
    string? PackageId = null,
    string? Path = null,
    string? Capability = null);

/// <summary>A file inside an imported, immutable package.</summary>
/// <param name="StoredPath">Path relative to the package's stored file root (library).</param>
/// <param name="Target">Normalized game-relative target path (GamePath.Value).</param>
public sealed record PackageFile(string StoredPath, string Target, string Hash, long Size);

public sealed record RequirementSpec(string Capability, bool Mandatory, string Origin, string Rule);

public static class PackageOrigin
{
    public const string Manual = "manual";
    public const string Nexus = "nexus";
    public const string Captured = "captured";
}

public sealed record PackageManifest(
    string PackageId,
    string ModId,
    string ModName,
    string Version,
    string Origin,
    string? LayerOfModId,
    string ManifestDigest,
    IReadOnlyList<PackageFile> Files,
    IReadOnlyList<string> Provides,
    IReadOnlyList<RequirementSpec> Requirements,
    string? Category = null,
    string? NexusModId = null,
    string? NexusFileId = null,
    string? SourceArchiveName = null,
    string? SourceMd5 = null,
    DateTimeOffset? ImportedAt = null)
{
    /// <summary>R3: options chosen in the mod's FOMOD installer (to reinstall or to replay a collection).</summary>
    public IReadOnlyList<FomodChoice>? FomodChoices { get; init; }
}

public sealed record ProfileEntry(string PackageId, bool Enabled);

public sealed record RequirementOverride(string PackageId, string Capability);

public sealed record ArchiveOrderSettings(
    bool Custom,
    IReadOnlyList<string> Order,
    bool IncludeUnmanagedArchives,
    bool AllowModlistTakeover)
{
    public static ArchiveOrderSettings Default { get; } = new(false, Array.Empty<string>(), false, false);
}

public sealed record ProfileState(
    string Id,
    string Name,
    bool IsSystemModsOff,
    long Revision,
    IReadOnlyList<ProfileEntry> Entries,
    IReadOnlyDictionary<string, string> PathWinners,
    IReadOnlyList<RequirementOverride> Overrides,
    ArchiveOrderSettings ArchiveOrder)
{
    public const string ModsOffId = "mods-off";

    public static ProfileState CreateModsOff() => new(
        ModsOffId, "Mods off", true, 1, Array.Empty<ProfileEntry>(),
        new Dictionary<string, string>(), Array.Empty<RequirementOverride>(), ArchiveOrderSettings.Default);

    public static ProfileState Create(string id, string name) => new(
        id, name, false, 1, Array.Empty<ProfileEntry>(),
        new Dictionary<string, string>(), Array.Empty<RequirementOverride>(), ArchiveOrderSettings.Default);

    public bool IsEnabled(string packageId) => Entries.Any(e => e.PackageId == packageId && e.Enabled);

    public ProfileState WithEntry(string packageId, bool enabled)
    {
        if (IsSystemModsOff) throw new InvalidOperationException("Mods off is a system profile and cannot be edited.");
        var list = Entries.Where(e => e.PackageId != packageId).ToList();
        var existingIndex = Entries.ToList().FindIndex(e => e.PackageId == packageId);
        var entry = new ProfileEntry(packageId, enabled);
        if (existingIndex >= 0 && existingIndex <= list.Count) list.Insert(existingIndex, entry); else list.Add(entry);
        return this with { Entries = list, Revision = Revision + 1 };
    }

    public ProfileState WithoutEntry(string packageId)
    {
        if (IsSystemModsOff) throw new InvalidOperationException("Mods off is a system profile and cannot be edited.");
        return this with { Entries = Entries.Where(e => e.PackageId != packageId).ToList(), Revision = Revision + 1 };
    }

    public ProfileState WithWinner(string pathKey, string packageId)
    {
        if (IsSystemModsOff) throw new InvalidOperationException("Mods off is a system profile and cannot be edited.");
        var d = new Dictionary<string, string>(PathWinners) { [pathKey] = packageId };
        return this with { PathWinners = d, Revision = Revision + 1 };
    }

    public ProfileState WithOverride(string packageId, string capability)
    {
        if (IsSystemModsOff) throw new InvalidOperationException("Mods off is a system profile and cannot be edited.");
        if (Overrides.Any(o => o.PackageId == packageId && o.Capability == capability)) return this;
        return this with { Overrides = Overrides.Append(new RequirementOverride(packageId, capability)).ToList(), Revision = Revision + 1 };
    }

    public ProfileState WithArchiveOrder(ArchiveOrderSettings settings)
    {
        if (IsSystemModsOff) throw new InvalidOperationException("Mods off is a system profile and cannot be edited.");
        return this with { ArchiveOrder = settings, Revision = Revision + 1 };
    }
}

public static class ContentHash
{
    public static string Of(ReadOnlySpan<byte> data) => Convert.ToHexStringLower(XxHash128.Hash(data));

    public static string Of(string text) => Of(Encoding.UTF8.GetBytes(text));

    public static string Of(Stream stream)
    {
        var h = new XxHash128();
        var buffer = new byte[1 << 20];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) h.Append(buffer.AsSpan(0, read));
        return Convert.ToHexStringLower(h.GetHashAndReset());
    }

    public static string ManifestDigest(IEnumerable<PackageFile> files)
    {
        var sb = new StringBuilder();
        foreach (var f in files.OrderBy(f => f.Target.ToUpperInvariant(), StringComparer.Ordinal))
            sb.Append(f.Target.ToUpperInvariant()).Append('\t').Append(f.Hash).Append('\t').Append(f.Size).Append('\n');
        return Of(sb.ToString());
    }
}
