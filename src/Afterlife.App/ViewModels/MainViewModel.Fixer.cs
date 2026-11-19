using Afterlife.Infrastructure;

namespace Afterlife.App.ViewModels;

/// <summary>
/// The drop box for FIXER (the quest editor): while it is switched on in Settings, the window looks into
/// fixer/inbox of the library every few seconds and takes what FIXER left there. See <see cref="FixerService"/>.
/// </summary>
public sealed partial class MainViewModel
{
    private System.Windows.Threading.DispatcherTimer? _fixerTimer;
    private bool _fixerOn;
    private bool _fixerTaking;

    private static string EditionId => AppEdition.Offline ? "offline" : "full";

    /// <summary>After the library opens: tell FIXER whether the drop box is on and take what waited.</summary>
    private async Task StartFixerAsync()
    {
        try { _fixerOn = await _host.RunAsync(c => { c.Fixer.Announce(EditionId, AppVersion); return c.Fixer.Enabled; }); }
        catch (Exception ex) when (IsExpected(ex)) { _fixerOn = false; }
        WatchFixer();
        await TakeFromFixerAsync();
    }

    private void WatchFixer()
    {
        if (!_fixerOn) { _fixerTimer?.Stop(); return; }
        if (_fixerTimer is null)
        {
            // FIXER waits half a minute for the answer, and AFTERLIFE is usually not the window in front then
            _fixerTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _fixerTimer.Tick += async (_, _) => await TakeFromFixerAsync();
        }
        _fixerTimer.Start();
    }

    /// <summary>Never over a running operation, a dialog or a change that waits for recovery; the next tick tries again.</summary>
    private async Task TakeFromFixerAsync()
    {
        if (!_fixerOn || _fixerTaking || !_host.IsOpen || IsBusy || IsOperating || Overlay is not null || RecoveryRequired) return;
        if (_host.Library is not { } library || !FixerService.HasPending(library.Root)) return;
        _fixerTaking = true;
        try
        {
            await RunUiAsync(async () =>
            {
                var results = await _host.RunAsync(c => c.Fixer.Process());
                if (results.Count == 0) return;
                await _host.RunAsync(c => c.RefreshRequirements());
                await RefreshAsync();
                var taken = results.LastOrDefault(r => r.Status == FixerResult.Imported)?.PackageId;
                if (taken is not null) SelectedMod = Mods.FirstOrDefault(m => m.PackageId == taken) ?? SelectedMod;
                var rejected = results.Count(r => r.Status == FixerResult.Rejected);
                SayLines(results.Select(r => r.Status == FixerResult.Rejected ? $"FIXER · {r.File} was not taken: {r.Message}" : $"FIXER · {r.Message}").ToList(),
                    rejected == 0 ? Tone.Ok : rejected == results.Count ? Tone.Block : Tone.Warn);
            });
        }
        finally { _fixerTaking = false; }
    }

    /// <summary>After a change of the game: FIXER learns which quests are in the game, old builds leave the library.</summary>
    private async Task FixerAfterApplyAsync()
    {
        if (!_fixerOn) return;
        try { await _host.RunAsync(c => c.Fixer.AfterApply()); }
        catch (Exception ex) when (IsExpected(ex)) { }
    }

    private async Task SetFixerAsync(SettingsVm vm, bool on)
    {
        try
        {
            await _host.RunAsync(c => c.Fixer.SetEnabled(on, EditionId, AppVersion));
            _fixerOn = on;
            WatchFixer();
            vm.Message = on
                ? "FIXER drop box is on: quests sent from FIXER arrive in the category FIXER, switched on in the selected profile."
                : "FIXER drop box is off. Quests already in the library stay as they are.";
            vm.MessageTone = Tone.Ok;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            vm.SetFixerSilently(!on);
            vm.Message = ex.Message;
            vm.MessageTone = Tone.Block;
        }
    }
}
