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

/// <summary>Apply, recovery and launching the game.</summary>
public sealed partial class MainViewModel
{
    /// <summary>Asks before writing to the game (deck).</summary>
    [RelayCommand]
    private Task ApplyChanges() => RunUiAsync(async () =>
    {
        if (_overview is null) return;
        var name = SelectedProfile?.Name ?? "";
        var added = _overview.Mods.Count(m => m.Wanted && !m.InGame);
        var removed = _overview.Mods.Count(m => !m.Wanted && m.InGame);
        var lines = new List<string>();
        if (added > 0) lines.Add($"+ {added} mod(s) into the game");
        if (removed > 0) lines.Add($"− {removed} mod(s) out of the game");
        // mods held back because a framework they need is off lose their files too; name the reason
        var heldBack = _overview.Preview.Snapshot.Issues
            .Where(i => i.Kind == IssueKind.MissingRequirement && i.Severity == Severity.Blocking && i.PackageId is not null
                        && _overview.Preview.Plan.Steps.Any(s => s.Kind == StepKind.Remove && s.Owner == i.PackageId))
            .GroupBy(i => i.Capability ?? "?").ToList();
        foreach (var g in heldBack)
            lines.Add($"− {g.Count()} mod(s) held back: {Capabilities.DisplayName(g.Key)} is off, so their files leave the game too");
        if (_overview.OrderIsCustom) lines.Add("load order from the list (modlist.txt)");
        lines.AddRange(TwinWarnings()); // two versions of one mod would go into the game
        foreach (var n in LeavingCore().Select(c => c.Name)) lines.Add($"{n}: files it made itself (logs, settings) go to the library, restorable");
        var msg = IsModsOff
            ? "Every mod AFTERLIFE put in the game will be taken out. Manually installed mods stay."
            : $"Load {name} to the deck? Only files AFTERLIFE knows are written; every file is verified afterwards.";
        // "Load to deck"
        if (!await AskAsync(new ConfirmVm(IsModsOff ? "SWITCH THE GAME TO MODS OFF?" : $"LOAD {name.ToUpperInvariant()} TO THE DECK?", msg,
                IsModsOff ? "MODS OFF" : "LOAD TO DECK", lines: lines))) return;
        await ApplyCoreAsync(PlanDecisions.None);
    });

    [RelayCommand]
    private Task Preview() => RunUiAsync(async () =>
    {
        var r = await _host.RunAsync(c => c.Preview());
        Overlay = new PlanPreviewVm(SelectedProfile?.Name ?? "", r, id => _overview?.Mods.FirstOrDefault(m => m.Package.PackageId == id)?.Package.ModName)
            { CloseCommand = new RelayCommand(() => Overlay = null) };
    });

    /// <summary>Core mods that the next apply takes out of the game.</summary>
    private List<(string Capability, string Name)> LeavingCore()
    {
        if (_overview is null) return [];
        return _overview.Mods.Where(m => m.IsCore && m.InGame && (!m.Wanted || IsModsOff))
            .SelectMany(m => m.Package.Provides.Where(c => c.StartsWith("framework:", StringComparison.Ordinal)))
            .Distinct().Select(c => (c, Capabilities.DisplayName(c))).ToList();
    }

    /// <summary>After a core mod left the game, its own runtime files are moved aside (restorable).</summary>
    private async Task<List<string>> CleanLeftoversAsync(IEnumerable<string> capabilities)
    {
        var lines = new List<string>();
        foreach (var cap in capabilities.Distinct())
        {
            try
            {
                var r = await _host.RunAsync(c => c.CleanLeftovers(cap));
                if (r is { Moved: > 0 }) lines.Add($"{Capabilities.DisplayName(cap)}: moved {r.Moved} file(s) it made itself to the library (restore in Scan manual mods).");
            }
            catch (Exception ex) when (IsExpected(ex)) { lines.Add($"{Capabilities.DisplayName(cap)}: its own files stayed in the game ({ex.Message})."); }
        }
        return lines;
    }

    private async Task ApplyCoreAsync(PlanDecisions decisions)
    {
        var leaving = LeavingCore().Select(c => c.Capability).ToList();
        IsOperating = true;
        CanApply = false;
        CanLaunch = false;
        BeginBreach($"WRITING · {SelectedProfile?.Name}", _overview?.PendingChanges ?? 0);
        SetPhase(DeployPhase.Preparing, 0, 0);
        try
        {
            var progress = new Progress<DeployProgress>(p => SetPhase(p.Phase, p.Done, p.Total, p.Path, p.Kind));
            var r = await _host.RunAsync(c => c.Apply(null, decisions, progress));
            if (r.Succeeded)
            {
                SetPhase(DeployPhase.Done, 0, 0);
                EndBreach(true, r.Execution is null ? "nothing to write" : $"{r.Execution.StepsApplied} change(s) verified");
                var done = new List<string> { r.Message == "already deployed" ? "The game already matches this profile." : "Done. The game matches the profile and every file was verified." };
                done.AddRange(await CleanLeftoversAsync(leaving));
                SayLines(done, Tone.Ok);
                ScanCompleted?.Invoke();
            }
            else if (r.Execution is not null)
            {
                PhaseFailed();
                EndBreach(false, "recovery needed");
                Say("The change did not finish: " + r.Message, Tone.Block);
            }
            else
            {
                PhaseIdle();
                CancelBreach();
                var modified = r.Preview?.Plan.Blockers.Where(b => b.Kind == IssueKind.ModifiedManagedFile && b.Path is not null).ToList() ?? [];
                if (modified.Count > 0 && decisions.DiscardChangesPathKeys.Count == 0)
                {
                    string NameOf(string? id) => _overview?.Mods.FirstOrDefault(m => m.Package.PackageId == id)?.Package.ModName ?? "Other files";
                    var items = await AskAsync(new DecisionsVm(modified.Select(b => (b.Path!, NameOf(b.PackageId)))));
                    if (items is null) { Say("Nothing was changed.", Tone.Neutral); return; }
                    var keep = items.Where(i => i.Keep).Select(i => i.Path).ToList();
                    var discard = items.Where(i => !i.Keep).Select(i => GamePath.Parse(i.Path).Key).ToHashSet();
                    if (keep.Count > 0) await _host.RunAsync(c => c.CaptureChanges(keep));
                    await ApplyCoreAsync(new PlanDecisions(discard));
                    return;
                }
                Say("Nothing was changed: " + r.Message.Replace("blocked: ", ""), Tone.Block);
            }
        }
        catch (InstallBusyException ex)
        {
            PhaseIdle();
            CancelBreach();
            Say(ex.Message, Tone.Warn);
        }
        catch
        {
            PhaseFailed();
            EndBreach(false, "stopped");
            throw;
        }
        finally
        {
            IsOperating = false;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private Task RecoverFinish() => RecoverAsync(RecoveryChoice.Finish);

    [RelayCommand]
    private Task RecoverReturn() => RecoverAsync(RecoveryChoice.ReturnToPreviousGeneration);

    private Task RecoverAsync(RecoveryChoice choice) => RunUiAsync(async () =>
    {
        IsOperating = true;
        BeginBreach(choice == RecoveryChoice.Finish ? "RECOVERY · finishing" : "RECOVERY · returning", 0);
        try
        {
            var progress = new Progress<DeployProgress>(p => SetPhase(p.Phase, p.Done, p.Total, p.Path, p.Kind));
            var (outcome, follow) = await _host.RunAsync(c => c.Recover(choice, progress));
            EndBreach(follow.Succeeded && outcome.UnknownPaths.Count == 0, follow.Succeeded ? "state verified" : "needs attention");
            if (outcome.UnknownPaths.Count > 0)
                Say($"Recovered the record, but {outcome.UnknownPaths.Count} file(s) have unknown content and were left untouched: {string.Join(", ", outcome.UnknownPaths.Take(3))}. Check them in Inbox.", Tone.Warn);
            else if (follow.Succeeded) { SetPhase(DeployPhase.Done, 0, 0); Say("Recovered. The game is in a known, verified state.", Tone.Ok); }
            else { PhaseFailed(); Say("Recovery needs attention: " + follow.Message, Tone.Block); }
        }
        finally
        {
            IsOperating = false;
            await RefreshAsync();
        }
    });

    [RelayCommand]
    private Task Launch() => LaunchCoreAsync(false);

    [RelayCommand]
    private Task LaunchAnyway() => RunUiAsync(async () =>
    {
        var ok = await AskAsync(new ConfirmVm("LAUNCH ANYWAY?",
            "The game was updated since you last reviewed it. Mods may crash or behave oddly until they are updated. Nothing will be switched off.",
            "LAUNCH ANYWAY"));
        if (ok) await LaunchCoreAsync(true);
    });

    private Task LaunchCoreAsync(bool allowChangedBuild) => RunUiAsync(async () =>
    {
        // REDmods are compiled by redMod.exe and need -modded
        var mods = await _host.RunAsync(c => c.DeployedRedMods());
        var args = "--launcher-skip";
        string? note = null;
        if (mods.Count > 0)
        {
            Say($"Deploying {mods.Count} REDmod(s)… this can take a few minutes the first time.", Tone.Info);
            IsOperating = true;
            RedModResult r;
            try { r = await _host.RunAsync(c => c.DeployRedMods()); }
            finally { IsOperating = false; }
            if (r.LaunchModded) args += " -modded";
            else if (r.Outcome == RedModOutcome.Failed)
            {
                var go = await AskAsync(new ConfirmVm("REDMOD DEPLOY FAILED", r.Message + " Start the game without the REDmods?", "START WITHOUT REDMODS", false,
                    mods.Select(m => "mods/" + m).ToList(), null, true, "REDMODS"));
                if (!go) { Say(r.Message, Tone.Block); return; }
            }
            note = r.Outcome == RedModOutcome.ToolMissing ? r.Message : r.Outcome == RedModOutcome.Deployed ? r.Message + "." : null;
        }
        await _host.RunAsync(c => c.LaunchGame(args, allowChangedBuild));
        if (note is null) Say("Wake up, choom. The game is starting.", Tone.Ok);
        else SayLines(["Wake up, choom. The game is starting.", note], Tone.Ok);
    });

    [RelayCommand]
    private Task AcknowledgeBuild() => RunUiAsync(async () =>
    {
        var ok = await AskAsync(new ConfirmVm("MARK BUILD AS REVIEWED?",
            "Confirm that you checked your mods for the new game version (for example on their Nexus pages). This only removes the warning; nothing else changes.",
            "MARK AS REVIEWED"));
        if (!ok) return;
        await _host.RunAsync(c => c.AcknowledgeBuild());
        await RefreshAsync();
    });

    [RelayCommand]
    private Task SwitchToModsOff() => RunUiAsync(async () =>
    {
        await _host.RunAsync(c => c.SelectProfile(ProfileState.ModsOffId));
        await RefreshAsync();
        await ApplyCoreAsync(PlanDecisions.None);
    });
}
