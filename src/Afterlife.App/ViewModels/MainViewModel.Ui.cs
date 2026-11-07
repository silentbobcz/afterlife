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

/// <summary>Overlays, notices, toasts and the BREACH progress panel.</summary>
public sealed partial class MainViewModel
{
    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    /// <summary>Short notice at the bottom of the window that disappears after 2 seconds.</summary>
    public void ShowToast(string text)
    {
        Toast = text;
        _toastTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _toastTimer.Stop();
        _toastTimer.Tick -= ToastTick;
        _toastTimer.Tick += ToastTick;
        _toastTimer.Start();
    }

    private void ToastTick(object? sender, EventArgs e) { _toastTimer?.Stop(); Toast = null; }

    public void CopyToClipboard(string text)
    {
        try { System.Windows.Clipboard.SetText(text); ShowToast($"Copied to clipboard: {text}"); }
        catch (System.Runtime.InteropServices.ExternalException) { ShowToast("The clipboard is busy; try again."); }
    }

    [RelayCommand] private void DismissNotice() { Notice = null; NoticeLines.Clear(); }
    private async Task<T> AskAsync<T>(OverlayVm vm, Func<OverlayVm, Task<T>> result)
    {
        var previous = Overlay;
        Overlay = vm;
        try { return await result(vm); }
        finally { Overlay = previous; }
    }

    private Task<bool> AskAsync(ConfirmVm vm) => AskAsync(vm, v => ((ConfirmVm)v).Result);

    private Task<string?> AskAsync(TextPromptVm vm) => AskAsync(vm, v => ((TextPromptVm)v).Result);

    private Task<string?> AskAsync(VariantChoiceVm vm) => AskAsync(vm, v => ((VariantChoiceVm)v).Result);

    private Task<IReadOnlyList<FomodChoice>?> AskAsync(FomodVm vm) => AskAsync(vm, v => ((FomodVm)v).Result);

    private Task<IReadOnlyList<DecisionItemVm>?> AskAsync(DecisionsVm vm) => AskAsync(vm, v => ((DecisionsVm)v).Result);

    private void Say(string text, Tone tone) => SayLines([text], tone);

    /// <summary>Each item on its own line; long reports are cut to 12 lines.</summary>
    private void SayLines(IReadOnlyList<string> lines, Tone tone)
    {
        NoticeLines.Clear();
        var shown = lines.Count > 12 ? lines.Take(11).Append($"  … and {lines.Count - 11} more").ToList() : lines.ToList();
        foreach (var l in shown) NoticeLines.Add(l);
        Notice = string.Join("\n", shown);
        NoticeTone = tone;
        ActivityLog.Write(tone == Tone.Block ? "error" : "notice", string.Join("\n", lines));
    }

    private void BeginBreach(string title, int total)
    {
        _breachRun++;
        BreachLines.Clear();
        BreachTitle = title;
        BreachTone = Tone.Info;
        BreachDone = 0;
        BreachTotal = Math.Max(total, 1);
        BreachActive = true;
        ShowBreach = true;
        AddBreachLine("> JACKING INTO GAME FOLDER", Tone.Neutral);
    }

    private void AddBreachLine(string text, Tone tone)
    {
        BreachLines.Add(new LineVm(text, "", tone));
        while (BreachLines.Count > 9) BreachLines.RemoveAt(0);
    }

    private async void EndBreach(bool ok, string detail)
    {
        var run = _breachRun;
        BreachActive = false;
        BreachDone = BreachTotal;
        BreachTone = ok ? Tone.Ok : Tone.Block;
        BreachTitle = ok ? $"ACCESS GRANTED · {detail}" : $"BREACH INTERRUPTED · {detail}";
        AddBreachLine(ok ? "✓ VERIFIED" : "× STOPPED · nothing unknown was overwritten", ok ? Tone.Ok : Tone.Block);
        await Task.Delay(ok ? 1600 : 4500);
        if (run == _breachRun) ShowBreach = false;
    }

    private void CancelBreach()
    {
        _breachRun++;
        BreachActive = false;
        ShowBreach = false;
    }

    private static string Verb(StepKind? k) => k switch
    {
        StepKind.Add => "WRITE  ",
        StepKind.Replace => "INJECT ",
        StepKind.Remove => "PURGE  ",
        _ => "SYNC   ",
    };

    private void SetPhase(DeployPhase phase, int done, int total, string? path = null, StepKind? kind = null)
    {
        if (ShowBreach)
        {
            if (total > 0) { BreachTotal = total; BreachDone = done; }
            if (path is not null) AddBreachLine($"> {Verb(kind)} {path}", kind == StepKind.Remove ? Tone.Warn : Tone.Info);
            else if (phase == DeployPhase.Verifying) AddBreachLine("> VERIFY  hashes of every written file", Tone.Neutral);
        }
        var profile = SelectedProfile?.Name ?? "";
        switch (phase)
        {
            case DeployPhase.Preparing:
                PhaseTitle = $"PREPARING · {profile}"; PhaseTone = Tone.Info;
                Seg1 = Tone.Info; Seg2 = Tone.Neutral; Seg3 = Tone.Neutral; Seg2Label = "Applying";
                break;
            case DeployPhase.Applying:
                PhaseTitle = $"APPLYING · {profile}"; PhaseTone = Tone.Info;
                Seg1 = Tone.Ok; Seg2 = Tone.Info; Seg3 = Tone.Neutral; Seg2Label = total > 0 ? $"Applying {done}/{total}" : "Applying";
                break;
            case DeployPhase.Verifying:
                PhaseTitle = $"VERIFYING · {profile}"; PhaseTone = Tone.Info;
                Seg1 = Tone.Ok; Seg2 = Tone.Ok; Seg3 = Tone.Info;
                break;
            case DeployPhase.Done:
                PhaseTitle = $"VERIFIED · {profile}"; PhaseTone = Tone.Ok;
                Seg1 = Tone.Ok; Seg2 = Tone.Ok; Seg3 = Tone.Ok; Seg2Label = "Applying";
                break;
        }
    }

    private void PhaseIdle()
    {
        PhaseTitle = "READY"; PhaseTone = Tone.Neutral;
        Seg1 = Seg2 = Seg3 = Tone.Neutral; Seg2Label = "Applying";
    }

    private void PhaseFailed()
    {
        PhaseTitle = "STOPPED · recovery needed"; PhaseTone = Tone.Block;
        if (Seg2 == Tone.Info) Seg2 = Tone.Block; else if (Seg3 == Tone.Info) Seg3 = Tone.Block; else Seg1 = Tone.Block;
    }

    private async Task RunUiAsync(Func<Task> work)
    {
        IsBusy = true;
        try { await work(); }
        catch (Exception ex) when (IsExpected(ex)) { Say(ex.Message, Tone.Block); }
        finally { IsBusy = false; }
    }
}
