using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>
/// P98: a new version of a mod in a collection; two versions of one mod on at the same time.
/// P99: duplicates (the same files twice in the library). P101: renaming profiles.
/// </summary>
public sealed partial class MainViewModel
{
    // ---------------- P98: two versions on ----------------
    /// <summary>Packages of the same mod (same AFTERLIFE mod id) that are on together in the selected profile.</summary>
    private static Dictionary<string, List<ModOverview>> TwinsOn(InstallOverview o) =>
        o.Mods.Where(m => m.Wanted && m.Package.LayerOfModId is null)
            .GroupBy(m => m.Package.ModId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Package.ImportedAt ?? DateTimeOffset.MinValue).ToList(), StringComparer.Ordinal);

    private void MarkTwins(IEnumerable<ModRowVm> rows, InstallOverview o)
    {
        var twins = TwinsOn(o);
        foreach (var r in rows)
            r.TwinText = twins.TryGetValue(r.Overview.Package.ModId, out var g) && g.Any(m => m.Package.PackageId == r.PackageId)
                ? $"{g.Count} VERSIONS ON" : null;
    }

    /// <summary>When a mod is switched on and another version of it is on: offer to switch the other one off.</summary>
    private async Task<IReadOnlyList<string>> OtherVersionsToSwitchOffAsync(string packageId)
    {
        if (_overview is null) return [];
        var me = _overview.Mods.FirstOrDefault(m => m.Package.PackageId == packageId);
        if (me is null || me.Package.LayerOfModId is not null) return [];
        var others = _overview.Mods.Where(m => m.Wanted && m.Package.PackageId != packageId && m.Package.LayerOfModId is null
                                               && m.Package.ModId == me.Package.ModId).ToList();
        if (others.Count == 0) return [];
        var off = await AskAsync(new ConfirmVm("ANOTHER VERSION IS ON",
            $"{me.Package.ModName} {me.Package.Version} is another version of a mod that is already on in this profile. Two versions of one mod in the game usually fight over the same files. Switch the other version off? (Keep both only when it is an optional add-on file from the same Nexus page.)",
            "SWITCH THE OTHER OFF", false, others.Select(o => $"{o.Package.ModName} {o.Package.Version}   → off").ToList()) { CancelLabel = "Keep both" });
        return off ? others.Select(o => o.Package.PackageId).ToList() : [];
    }

    /// <summary>Lines for the Apply confirmation when two versions of a mod would go into the game.</summary>
    private IEnumerable<string> TwinWarnings() =>
        _overview is null || IsModsOff ? []
            : TwinsOn(_overview).Values.Select(g => $"! {g[0].Package.ModName}: {g.Count} versions on ({string.Join(", ", g.Select(m => m.Package.Version))})");

    // ---------------- P98: new version of a mod in a collection ----------------
    private int? _collectionUpdateChoice; // P98: "for all" during Update all

    private IReadOnlyList<string> CollectionsOf(string packageId) =>
        _collections.Where(r => r.PackageOf.Values.Contains(packageId)).Select(r => r.Name).ToList();

    /// <summary>
    /// The old version is in a collection: 0 = new one into the collection, old one switched off;
    /// 1 = new one into the collection, old one deleted; 2 = new one stays outside the collection. Null = cancelled.
    /// </summary>
    private async Task<int?> AskCollectionUpdateAsync(PackageManifest older, PackageManifest newer, IReadOnlyList<string> collections, bool batch)
    {
        if (batch && _collectionUpdateChoice is { } remembered) return remembered;
        var vm = new ChoiceVm("NEW VERSION OF A COLLECTION MOD",
            $"{older.ModName} {older.Version} is part of {string.Join(", ", collections)}. What should {newer.Version} do?",
            [
                "Put it into the collection, switch the old version off (the old one stays in the library)",
                "Replace: put it into the collection and delete the old version from the library",
                "Keep it outside the collection (in MODS); the collection keeps the old version",
            ], "CONTINUE", batch ? "Use this for the other collection mods of this update" : null);
        var pick = await AskAsync(vm, v => ((ChoiceVm)v).Result);
        if (batch && pick is not null && vm.OptionChecked) _collectionUpdateChoice = pick;
        return pick;
    }

    // ---------------- P99: duplicates ----------------
    private bool _duplicatesAsked;

    /// <summary>Asks once per session (and after imports) to merge packages that have exactly the same files.</summary>
    private Task CheckDuplicatesAsync(bool force = false) => RunUiAsync(async () =>
    {
        if (_overview is null || (_duplicatesAsked && !force) || Overlay is not null) return;
        _duplicatesAsked = true;
        var groups = await _host.RunAsync(c => c.DuplicateGroups());
        if (groups.Count == 0) return;
        var byId = _overview.Mods.ToDictionary(m => m.Package.PackageId);
        var selected = _overview.Status.Selected;
        // keep the copy that is in the game, then the one that is on, then the one in a collection, then the oldest
        PackageManifest Keeper(IReadOnlyList<PackageManifest> g) => g
            .OrderByDescending(p => byId.TryGetValue(p.PackageId, out var m) && m.InGame)
            .ThenByDescending(p => selected.IsEnabled(p.PackageId))
            .ThenByDescending(p => CollectionsOf(p.PackageId).Count > 0)
            .ThenBy(p => p.ImportedAt ?? DateTimeOffset.MinValue).First();
        var plan = groups.Select(g => (Keep: Keeper(g), Drop: g.Where(p => p.PackageId != Keeper(g).PackageId).ToList())).ToList();
        var lines = plan.Select(x => $"{x.Keep.ModName} {x.Keep.Version}   × {x.Drop.Count + 1} → 1").ToList();
        var ok = await AskAsync(new ConfirmVm("DUPLICATE MODS IN THE LIBRARY",
            $"{plan.Count} mod(s) are in the library more than once with exactly the same files. Merge them: one copy stays (with its place in profiles, collections, categories and the load order), the other copies are deleted from the library. Files in the game stay as they are.",
            "MERGE", false, lines) { CancelLabel = "Not now" });
        if (!ok) return;
        var drop = plan.SelectMany(x => x.Drop.Select(d => d.PackageId)).ToList();
        var layout = _layout;
        foreach (var (keep, others) in plan)
        {
            var ids = others.Select(o => o.PackageId).ToList();
            await _host.RunAsync(c => c.AdoptDuplicates(keep.PackageId, ids));
            if (layout.CategoryIdOf(keep.PackageId) is null && ids.Select(layout.CategoryIdOf).FirstOrDefault(c => c is not null) is { } cat)
                layout = layout.Assign([keep.PackageId], cat);
        }
        if (!ReferenceEquals(layout, _layout)) await SaveLayoutAsync(layout);
        var r = await _host.RunAsync(c => c.DeleteEverywhere(drop));
        await RefreshAsync();
        var report = new List<string> { $"Merged {plan.Count} duplicate mod(s); {r.Deleted.Count} extra cop(ies) deleted." };
        if (r.Kept.Count > 0) report.AddRange(r.Kept.Select(k => $"  · not deleted {k.Name}: {k.Reason}"));
        SayLines(report, r.Kept.Count > 0 ? Tone.Warn : Tone.Ok);
    });

    // ---------------- P101: rename a profile ----------------
    [RelayCommand]
    private Task RenameProfile() => RunUiAsync(async () =>
    {
        if (SelectedProfile is not { IsSystem: false } p) { Say("Mods off cannot be renamed.", Tone.Warn); return; }
        var current = p.Name;
        while (true)
        {
            var name = await AskAsync(new TextPromptVm("RENAME PROFILE", "Profile name", current) { ConfirmLabel = "RENAME" });
            if (string.IsNullOrWhiteSpace(name) || name == p.Name) return;
            try
            {
                await _host.RunAsync(c => c.RenameProfile(p.Id, name));
                await RefreshAsync();
                Say($"Profile {p.Name} is now {name}.", Tone.Ok);
                return;
            }
            catch (InvalidOperationException ex) { Say(ex.Message, Tone.Warn); current = name; }
        }
    });

    /// <summary>P101: double-click on a category name.</summary>
    public void RenameCategory(string id) => _ = RenameCategoryAsync(id);
}
