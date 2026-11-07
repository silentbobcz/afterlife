using Afterlife.Core;

namespace Afterlife.Infrastructure;

/// <summary>Profiles of the library: which one is selected, what is on in it, its load order.</summary>
public sealed class ProfileService
{
    private readonly InstallContext _ctx;
    private AfterlifeStore Store => _ctx.Store;
    private ProfileState SelectedProfile => _ctx.SelectedProfile;

    internal ProfileService(InstallContext ctx) => _ctx = ctx;

    public void SelectProfile(string profileId)
    {
        if (Store.GetProfile(profileId) is null) throw new InvalidOperationException($"unknown profile {profileId}");
        Store.SetMeta("profile.selected", profileId);
    }

    public ProfileState CreateProfile(string name, string? copyFromId = null)
    {
        var id = "p-" + Guid.NewGuid().ToString("N")[..8];
        var baseProfile = copyFromId is null ? null : Store.GetProfile(copyFromId);
        var p = baseProfile is null || baseProfile.IsSystemModsOff
            ? ProfileState.Create(id, name)
            : baseProfile with { Id = id, Name = name, Revision = 1, IsSystemModsOff = false };
        Store.SaveProfile(p);
        return p;
    }

    /// <summary>
    /// Deletes a profile. Mods off cannot be deleted, nor the profile that is in the game now (switch the game
    /// To another profile first, so nothing in the game belongs to a missing profile). Mods stay in the library;
    /// A collection that used the profile keeps its record.
    /// </summary>
    public void DeleteProfile(string profileId)
    {
        var p = Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        if (p.IsSystemModsOff) throw new InvalidOperationException("Mods off cannot be deleted");
        if (Store.GetLastGeneration()?.ProfileId == profileId && Store.GetLedger().Count > 0)
            throw new InvalidOperationException($"{p.Name} is in the game now: select another profile and apply first");
        if (SelectedProfile.Id == profileId)
        {
            var next = Store.GetLastGeneration()?.ProfileId is { } d && d != profileId && Store.GetProfile(d) is not null ? d : ProfileState.ModsOffId;
            Store.SetMeta("profile.selected", next);
        }
        foreach (var r in CollectionStore.List(Store).Where(r => r.ProfileId == profileId))
            CollectionStore.Save(Store, r with { ProfileId = null });
        Store.DeleteProfile(profileId);
    }

    public ProfileState UpdateSelected(Func<ProfileState, ProfileState> change)
    {
        var p = SelectedProfile;
        if (p.IsSystemModsOff)
            throw new InvalidOperationException("Mods off cannot be edited; select or create another profile");
        var updated = change(p);
        Store.SaveProfile(updated);
        return updated;
    }

    public ProfileState SetEnabled(string packageId, bool enabled)
    {
        if (Store.GetPackage(packageId) is not { } pkg) throw new InvalidOperationException($"unknown package {packageId}");
        ActivityLog.Write("profile", $"{(enabled ? "on " : "off")} {pkg.ModName} [{packageId}] in '{SelectedProfile.Name}'");
        return UpdateSelected(p => p.WithEntry(packageId, enabled));
    }

    public ProfileState SetEnabledMany(string profileId, IReadOnlyCollection<string> packageIds, bool enabled)
    {
        var p = Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        if (p.IsSystemModsOff) throw new InvalidOperationException("Mods off cannot be edited; select or create another profile");
        foreach (var id in packageIds)
        {
            if (Store.GetPackage(id) is null) throw new InvalidOperationException($"unknown package {id}");
            p = p.WithEntry(id, enabled);
        }
        ActivityLog.Write("profile", $"{(enabled ? "on " : "off")} {packageIds.Count} mod(s) in '{p.Name}': {string.Join(", ", packageIds)}");
        Store.SaveProfile(p);
        return p;
    }

    /// <summary>A new name for a profile; Mods off keeps its name, two profiles cannot share one.</summary>
    public ProfileState RenameProfile(string profileId, string name)
    {
        var p = Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        if (p.IsSystemModsOff) throw new InvalidOperationException("Mods off cannot be renamed");
        name = name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("the name cannot be empty");
        if (Store.GetProfiles().Any(x => x.Id != profileId && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"a profile {name} already exists");
        var q = p with { Name = name };
        Store.SaveProfile(q);
        return q;
    }

    /// <summary>
    /// Drag-and-drop order of a profile: top loads first and wins archive conflicts. The order of the profile entries
    /// Follows the list and AFTERLIFE writes archive/pc/mod/modlist.txt from it. Archives installed outside AFTERLIFE
    /// Load after the listed ones; an existing modlist.txt is kept as a baseline and restored later.
    /// </summary>
    public ProfileState Reorder(string profileId, IReadOnlyList<string> orderedPackageIds)
    {
        var p = Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        if (p.IsSystemModsOff) throw new InvalidOperationException("Mods off cannot be edited; select or create another profile");
        var packages = Store.GetPackages().ToDictionary(x => x.PackageId);
        var ordered = orderedPackageIds.Where(packages.ContainsKey).Distinct().ToList();
        var byId = p.Entries.ToDictionary(e => e.PackageId);
        var entries = ordered.Select(id => byId.TryGetValue(id, out var e) ? e : new ProfileEntry(id, false)).ToList();
        entries.AddRange(p.Entries.Where(e => !ordered.Contains(e.PackageId)));
        var archives = ordered
            .SelectMany(id => packages[id].Files.Select(f => GamePath.Parse(f.Target))
                .Where(g => string.Equals(g.ParentValue, "archive/pc/mod", StringComparison.OrdinalIgnoreCase) && g.Extension == ".archive")
                .Select(g => g.FileName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var q = p with
        {
            Entries = entries,
            ArchiveOrder = new ArchiveOrderSettings(true, archives, true, true),
            Revision = p.Revision + 1,
        };
        Store.SaveProfile(q);
        return q;
    }

    /// <summary>Back to the game's default (alphabetical) archive order.</summary>
    public ProfileState ResetOrder(string profileId)
    {
        var p = Store.GetProfile(profileId) ?? throw new InvalidOperationException($"unknown profile {profileId}");
        if (p.IsSystemModsOff) throw new InvalidOperationException("Mods off cannot be edited; select or create another profile");
        var q = p.WithArchiveOrder(ArchiveOrderSettings.Default);
        Store.SaveProfile(q);
        return q;
    }
}
