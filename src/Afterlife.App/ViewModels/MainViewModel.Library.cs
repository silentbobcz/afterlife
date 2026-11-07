using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using Afterlife.App.Services;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Enable/disable, conflicts, multi-selection and bulk actions, Scan manual mods actions.</summary>
public sealed partial class MainViewModel
{
    private IReadOnlyList<ModRowVm> _multi = [];

    private async Task OnWantChangedAsync(ModRowVm row, bool want)
    {
        if (IsOperating) { row.ResetWant(!want); return; }
        try
        {
            var ids = await WithRelatedAsync([row.PackageId], want);
            if (ids is null) { row.ResetWant(!want); return; }
            // another version of the same mod is on: offer to switch it off
            var twinsOff = want ? await OtherVersionsToSwitchOffAsync(row.PackageId) : [];
            if (twinsOff is null) { row.ResetWant(!want); return; } // Cancel → nothing changes
            if (twinsOff.Count > 0) await _host.RunAsync(c => c.SetEnabledMany(c.SelectedProfile.Id, twinsOff, false));
            if (ids.Count == 1) await _host.RunAsync(c => c.SetEnabled(row.PackageId, want));
            else
            {
                await _host.RunAsync(c => c.SetEnabledMany(c.SelectedProfile.Id, ids, want));
                Say(want ? $"{row.Name} and {ids.Count - 1} mod(s) it needs are in the profile. Apply to put them in the game."
                         : $"{row.Name} and {ids.Count - 1} mod(s) that need it are switched off. Apply to remove them from the game.", Tone.Info);
            }
            await RefreshAsync();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            row.ResetWant(!want);
            Say(ex.Message, Tone.Block);
        }
    }

    public Task SetWantAsync(string packageId, bool want) => RunUiAsync(async () =>
    {
        var ids = await WithRelatedAsync([packageId], want);
        if (ids is null) return;
        await _host.RunAsync(c => c.SetEnabledMany(c.SelectedProfile.Id, ids, want));
        await RefreshAsync();
    });

    /// <summary>
    /// Switching on asks to switch on the needed mods that are off. switching off asks to switch off the
    /// Switched-on mods that need it. Returns the ids to change, or null when cancelled.
    /// </summary>
    private async Task<IReadOnlyList<string>?> WithRelatedAsync(IReadOnlyList<string> ids, bool on)
    {
        if (_overview is null || IsModsOff) return ids;
        var related = on ? OverviewBuilder.InactiveRequirements(_overview, ids) : OverviewBuilder.ActiveDependents(_overview, ids);
        if (related.Count == 0) return ids;
        var names = _overview.Mods.Where(m => ids.Contains(m.Package.PackageId)).Select(m => m.Package.ModName).ToList();
        var subject = names.Count == 1 ? names[0] : $"{names.Count} mods";
        var lines = on
            ? related.Select(r => $"{r.Name}   · needed by {r.NeededBy}").ToList()
            : related.Select(r => $"{r.Name}   · needs {r.NeededBy}").ToList();
        var vm = on
            ? new ConfirmVm("ALSO SWITCH ON WHAT IT NEEDS?",
                $"{subject} needs {related.Count} mod(s) that are in your library but switched off in this profile. Without them it may not work.",
                "SWITCH ON", false, lines, $"Also switch on the {related.Count} needed mod(s)", true, "NEEDED, NOW OFF")
            : new ConfirmVm("ALSO SWITCH OFF WHAT NEEDS IT?",
                $"{related.Count} switched-on mod(s) need {subject}. Without it they may not work or show errors in game.",
                "SWITCH OFF", false, lines, $"Also switch off the {related.Count} mod(s) that need it", true, "NEED IT, NOW ON");
        if (!await AskAsync(vm)) return null;
        return vm.OptionChecked ? ids.Concat(related.Select(r => r.PackageId)).Distinct().ToList() : ids;
    }

    public Task OverrideRequirementsAsync(string packageId, IReadOnlyList<string> capabilities) => RunUiAsync(async () =>
    {
        var ok = await AskAsync(new ConfirmVm("ENABLE ANYWAY?",
            "The mod will be deployed although a requirement is missing. It may not work and can show errors in game. This is shown as a warning until the requirement is in the profile.",
            "ENABLE ANYWAY"));
        if (!ok) return;
        await _host.RunAsync(c => c.UpdateSelected(p => capabilities.Aggregate(p, (q, cap) => q.WithOverride(packageId, cap))));
        await RefreshAsync();
    });

    /// <summary>This mod wins every conflict it has, with one click.</summary>
    public Task SetWinnersAsync(IReadOnlyList<string> paths, string packageId, string name) => RunUiAsync(async () =>
    {
        await _host.RunAsync(c => c.UpdateSelected(p => paths.Aggregate(p, (acc, path) => acc.WithWinner(GamePath.Parse(path).Key, packageId))));
        await RefreshAsync();
        Say($"{name} now wins {paths.Count} conflicting file(s). Apply to update the game.", Tone.Info);
    });

    public Task SetWinnerAsync(string path, string packageId) => RunUiAsync(async () =>
    {
        await _host.RunAsync(c => c.UpdateSelected(p => p.WithWinner(GamePath.Parse(path).Key, packageId)));
        await RefreshAsync();
        Say("Winner chosen. Apply to update the game.", Tone.Info);
    });

    public Task RemoveFromLibraryAsync(string packageId, string name) => DeleteManyAsync([packageId]);

    /// <summary>
    /// One question, then everything: switched off in every profile, taken out of the game (deck) and deleted
    /// From the library. Core mods also list the mods that need them.
    /// </summary>
    private Task DeleteManyAsync(IReadOnlyCollection<string> ids) => RunUiAsync(async () =>
    {
        if (_overview is null || ids.Count == 0) return;
        var rows = _overview.Mods.Where(m => ids.Contains(m.Package.PackageId)).ToList();
        var inGame = rows.Count(r => r.InGame);
        var profiles = _overview.Profiles.Where(p => !p.IsSystem).Select(p => p.Name).ToList();
        var lines = rows.Select(r => $"{r.Package.ModName}{(r.InGame ? "   · in game" : "")}").ToList();
        var caps = rows.SelectMany(r => r.Package.Provides).Where(c => c.StartsWith("framework:", StringComparison.Ordinal)).ToList();
        var dependents = caps.Count == 0 ? [] : await _host.RunAsync(c => c.DependentsOf(caps, ids));
        var msg = $"{(rows.Count == 1 ? rows[0].Package.ModName : $"{rows.Count} mods")} will be removed from every profile ({string.Join(", ", profiles)}), "
            + (inGame > 0 ? $"taken out of the game ({inGame} in game now) " : "")
            + "and deleted from the AFTERLIFE library. Your original archives on disk are not touched.";
        if (dependents.Count > 0)
        {
            lines.Add("");
            lines.Add("NEEDED BY:");
            lines.AddRange(dependents.Select(d => $"{d.Name}   · {(d.InGame ? "deployed" : string.Join(", ", d.Profiles))}"));
        }
        var vm = new ConfirmVm("DELETE EVERYWHERE?", msg, "DELETE EVERYWHERE", danger: true, lines,
            dependents.Count > 0 ? $"Also switch off the {dependents.Count} mod(s) that need it" : null, true,
            rows.Count == 1 && dependents.Count == 0 ? null : "WHAT HAPPENS");
        if (!await AskAsync(vm)) return;
        if (dependents.Count > 0 && vm.OptionChecked) await _host.RunAsync(c => c.DeactivateEverywhere(dependents.Select(d => d.PackageId).ToList()));

        IsOperating = true;
        var title = rows.Count == 1 ? rows[0].Package.ModName : $"{rows.Count} mods";
        if (inGame > 0) BeginBreach($"PURGE · {title}", rows.Sum(r => r.InstallMap.Count));
        try
        {
            var progress = new Progress<DeployProgress>(p => SetPhase(p.Phase, p.Done, p.Total, p.Path, p.Kind));
            var r = await _host.RunAsync(c => c.DeleteEverywhere(ids, progress));
            if (inGame > 0) EndBreach(r.Kept.Count == 0, r.Kept.Count == 0 ? "removed from game" : "needs attention");
            if (r.Execution is not null && r.Kept.Count == 0) SetPhase(DeployPhase.Done, 0, 0);
            var report = new List<string>();
            if (r.Deleted.Count > 0) report.Add($"Deleted everywhere ({r.Deleted.Count}):");
            report.AddRange(r.Deleted.Select(n => "  · " + n));
            if (r.Kept.Count > 0) report.Add($"Not deleted ({r.Kept.Count}):");
            report.AddRange(r.Kept.Select(k => $"  · {k.Name}: {k.Reason}"));
            if (dependents.Count > 0 && vm.OptionChecked) report.Add($"Switched off {dependents.Count} mod(s) that needed it.");
            if (r.Leftovers > 0) report.Add($"{r.Leftovers} file(s) the deleted mods had created themselves (logs, settings) moved to Removed.");
            if (r.Kept.Count == 0) report.AddRange(await CleanLeftoversAsync(caps));
            SayLines(report, r.Kept.Count > 0 ? Tone.Warn : Tone.Ok);
        }
        catch { if (inGame > 0) EndBreach(false, "stopped"); throw; }
        finally
        {
            IsOperating = false;
            await RefreshAsync();
        }
    });

    /// <summary>Selection is the checkboxes only: clicking a row shows its details and keeps the selection.</summary>
    private void UpdateChecked()
    {
        if (_building) return;
        _multi = Mods.Where(m => m.IsChecked).ToList();
        SelectedCount = _multi.Count;
        HasMultiSelection = _multi.Count > 0;
        var visible = Mods.Where(Visible).ToList();
        AllChecked = visible.Count > 0 && visible.All(m => m.IsChecked);
    }

    /// <summary>The row passes the search filter, so "select all" (list header, chapter header, Ctrl+A) takes it.</summary>
    private bool Visible(ModRowVm row) => ModsView.Filter?.Invoke(row) ?? true;

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    /// <summary>Select all = exactly the rows the search shows (hidden rows are cleared); false clears everything.</summary>
    public void SetAllChecked(bool value)
    {
        _building = true;
        try { foreach (var m in Mods) m.IsChecked = value && Visible(m); }
        finally { _building = false; }
        UpdateChecked();
    }

    [RelayCommand]
    private Task BulkActivate() => RunUiAsync(async () =>
    {
        IReadOnlyList<string>? ids = _multi.Select(m => m.PackageId).ToList();
        if (ids.Count == 0 || _overview is null) return;
        ids = await WithRelatedAsync(ids, true);
        if (ids is null) return;
        await _host.RunAsync(c => c.SetEnabledMany(_overview.Status.Selected.Id, ids, true));
        await RefreshAsync();
        Say($"{ids.Count} mods in profile {_overview.Status.Selected.Name}. Apply to put them in the game.", Tone.Info);
    });

    [RelayCommand]
    private Task BulkDeactivate() => RunUiAsync(async () =>
    {
        IReadOnlyList<string>? ids = _multi.Select(m => m.PackageId).ToList();
        if (ids.Count == 0 || _overview is null) return;
        ids = await WithRelatedAsync(ids, false);
        if (ids is null) return;
        await _host.RunAsync(c => c.SetEnabledMany(_overview.Status.Selected.Id, ids, false));
        await RefreshAsync();
        Say($"{ids.Count} mods switched off in {_overview.Status.Selected.Name}. Apply to remove them from the game.", Tone.Info);
    });

    [RelayCommand]
    private Task BulkDelete()
    {
        var ids = _multi.Select(m => m.PackageId).ToList();
        return ids.Count == 0 ? Task.CompletedTask : DeleteManyAsync(ids);
    }

    [RelayCommand]
    private void ClearSelection() => SetAllChecked(false);

    [RelayCommand]
    private Task ResetOrder() => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff) return;
        await _host.RunAsync(c => c.ResetOrder(_overview.Status.Selected.Id));
        await RefreshAsync();
        Say("Back to the game's default order (alphabetical). Apply to update the game.", Tone.Info);
    });

    public Task RemoveDetectedAsync(IReadOnlyList<DetectedMod> mods) => RunUiAsync(async () =>
    {
        if (mods.Count == 0) return;
        var files = mods.SelectMany(m => m.Files.Select(f => f.Path)).ToList();
        var label = mods.Count == 1 ? mods[0].Name : $"{mods.Count} mods";
        var lines = mods.Select(m => $"{m.Name}   · {m.Files.Count} file(s){(m.IsFramework ? " · core mod" : "")}").ToList();
        var caps = mods.Select(m => m.Capability).OfType<string>().ToList();
        var dependents = caps.Count == 0 ? [] : await _host.RunAsync(c => c.DependentsOf(caps));
        if (caps.Count > 0)
        {
            lines.Add("");
            lines.Add(dependents.Count == 0 ? "No AFTERLIFE mod needs these core mods. Manually installed mods may still need them." : "NEEDED BY:");
            lines.AddRange(dependents.Select(d => $"{d.Name}   · {(d.InGame ? "deployed" : string.Join(", ", d.Profiles))}"));
        }
        var vm = new ConfirmVm("REMOVE FROM GAME?",
            $"{label} ({files.Count} files) will be moved out of the game into the AFTERLIFE library (folder \"removed\"). Nothing is deleted; you can restore it at any time from Scan manual mods.",
            "REMOVE FROM GAME", danger: true, lines,
            dependents.Count > 0 ? $"Also switch off the {dependents.Count} mod(s) that need it" : null, true, "WHAT HAPPENS");
        if (!await AskAsync(vm)) return;
        if (dependents.Count > 0 && vm.OptionChecked)
        {
            await _host.RunAsync(c => c.DeactivateEverywhere(dependents.Select(d => d.PackageId).ToList()));
            if (dependents.Any(d => d.InGame)) Say("The mods that need it are switched off in every profile. Apply to take them out of the game.", Tone.Info);
        }
        Overlay = null; // show the breach panel; Inbox can be reopened
        BeginBreach($"PURGE · {label}", files.Count);
        try
        {
            var r = await _host.RunAsync(c => c.RemoveUnmanaged(files, label));
            foreach (var f in files.Take(9)) AddBreachLine($"> PURGE   {f}", Tone.Warn);
            EndBreach(r.Skipped.Count == 0, $"{r.Moved} file(s) moved out");
            await RefreshAsync();
            Say(r.Skipped.Count == 0
                ? $"Removed {label} from the game. Restore it from Scan manual mods if you need it back."
                : $"Removed {r.Moved} file(s); {r.Skipped.Count} changed meanwhile and stayed in the game.", r.Skipped.Count == 0 ? Tone.Ok : Tone.Warn);
        }
        catch { EndBreach(false, "stopped"); throw; }
    });

    /// <summary>
    /// REPLACE: the files in the way are moved into the library ("removed", restorable in Scan manual mods);
    /// Then the usual apply question installs the mod.
    /// </summary>
    public Task ReplaceForeignAsync(string packageId, string name, IReadOnlyList<string> paths) => RunUiAsync(async () =>
    {
        var vm = new ConfirmVm("REPLACE FILES IN THE GAME?",
            $"These files are in the game but were not written by AFTERLIFE. They will be moved into the library (folder \"removed\") and {name} takes their place. You can put them back any time in Scan manual mods.",
            "REPLACE", danger: true, paths, linesTitle: "MOVED ASIDE");
        if (!await AskAsync(vm)) return;
        BeginBreach($"PURGE · in the way of {name}", paths.Count);
        try
        {
            var r = await _host.RunAsync(c => c.MoveAsideFor(packageId));
            foreach (var f in paths.Take(9)) AddBreachLine($"> PURGE   {f}", Tone.Warn);
            EndBreach(r.Skipped.Count == 0, $"{r.Moved} file(s) moved aside");
            await RefreshAsync();
            var lines = new List<string> { $"Moved aside {r.Moved} file(s) for {name} (restorable in Scan manual mods)." };
            if (r.Skipped.Count > 0) lines.Add($"Skipped {r.Skipped.Count}: {string.Join(", ", r.Skipped.Take(3))}");
            lines.Add(_overview?.Mods.FirstOrDefault(m => m.Package.PackageId == packageId)?.Wanted == true
                ? "Apply to install it." : $"Switch {name} on and apply to install it.");
            SayLines(lines, r.Skipped.Count == 0 ? Tone.Ok : Tone.Warn);
        }
        catch { EndBreach(false, "stopped"); throw; }
    });

    public Task RestoreRemovedAsync(RemovedBatch batch) => RunUiAsync(async () =>
    {
        Overlay = null;
        BeginBreach($"RESTORE · {batch.Label}", batch.Pending);
        try
        {
            var r = await _host.RunAsync(c => c.RestoreRemoved(batch.Id));
            foreach (var f in batch.Files.Take(9)) AddBreachLine($"> RESTORE {f.Path}", Tone.Info);
            EndBreach(r.Skipped.Count == 0, $"{r.Moved} file(s) restored");
            await RefreshAsync();
            Say(r.Skipped.Count == 0 ? $"{batch.Label} is back in the game."
                : $"Restored {r.Moved} file(s). Skipped {r.Skipped.Count} because a file with the same name is in the game now: {string.Join(", ", r.Skipped.Take(3))}.",
                r.Skipped.Count == 0 ? Tone.Ok : Tone.Warn);
        }
        catch { EndBreach(false, "stopped"); throw; }
    });

    public Task RenameModAsync(string packageId) => RunUiAsync(async () =>
    {
        var m = _overview?.Mods.FirstOrDefault(x => x.Package.PackageId == packageId);
        if (m is null) return;
        var name = await AskAsync(new TextPromptVm("RENAME MOD", "Name", m.Package.ModName) { ConfirmLabel = "RENAME", MaxLength = 120, AllowEmpty = true });
        if (name is null || name == m.Package.ModName) return;
        await _host.RunAsync(c => c.RenamePackage(packageId, name));
        await RefreshAsync();
    });
}
