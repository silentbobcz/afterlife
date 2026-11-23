using Afterlife.App.ViewModels.Overlays;
using Afterlife.Infrastructure;

namespace Afterlife.App.ViewModels;

/// <summary>
/// MOVE LIBRARY in Settings and what a start does with a move that did not finish. The work itself is
/// <see cref="LibraryMover"/>; the game folder is never touched by it.
/// </summary>
public sealed partial class MainViewModel
{
    private static LibraryMover NewLibraryMover() => new(LibraryMover.DefaultStateFile, SaveLibraryRoot);

    private static void SaveLibraryRoot(string root) => (AppSettings.Load() with { LibraryRoot = root }).Save();

    private static string? MoveBlockedByEnvironment() =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AFTERLIFE_LIBRARY")) ? null
            : "The library folder is set by the AFTERLIFE_LIBRARY environment variable. Remove the variable to move the library from here.";

    /// <summary>Settings → Game &amp; library → MOVE…: check, ask, move, and open the library at its new place.</summary>
    private async Task MoveLibraryAsync(SettingsVm vm, string folder)
    {
        void Tell(string text, Tone tone) { vm.Message = text; vm.MessageTone = tone; }

        if (MoveBlockedByEnvironment() is { } blocked) { Tell(blocked, Tone.Block); return; }
        if (IsBusy) { Tell("AFTERLIFE is working on something else. Wait until it finishes, then move the library.", Tone.Warn); return; }
        var mover = NewLibraryMover();
        LibraryMovePlan plan;
        try
        {
            // a folder that already holds something (a drive, "Games") gets the library in a folder of its own
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
                folder = Path.Combine(folder, AppSettings.PortableLibraryFolderName);
            plan = await _host.RunAsync(c =>
            {
                if (c.Store.GetUnfinishedOperation() is not null)
                    throw new InvalidOperationException("A change to the game did not finish. Finish or undo it first, then move the library.");
                return mover.Check(c.Library.Root, folder, c.GameRoot);
            });
        }
        catch (Exception ex) when (IsExpected(ex)) { Tell(ex.Message, Tone.Block); return; }

        var confirm = new ConfirmVm("MOVE THE LIBRARY?",
            "AFTERLIFE moves everything it keeps (imported mods, profiles, downloads, removed files) to the new folder and uses it from now on. "
            + "The game folder is not touched: mods that are in the game stay there and keep working. Do not close AFTERLIFE while it moves.",
            "MOVE",
            lines:
            [
                $"From: {plan.From}",
                $"To: {plan.To}",
                $"Size: {SettingsVm.Size(plan.Bytes)} in {plan.Files} files",
                plan.SameVolume
                    ? "Same drive: the folder is renamed, which takes a moment"
                    : "Another drive: every file is copied and checked; the old folder is emptied only after that",
            ],
            linesTitle: "WHAT HAPPENS");
        if (!await AskAsync(confirm)) return;

        string? failed = null;
        await RunUiAsync(async () =>
        {
            Tell("Moving the library…", Tone.Info);
            var last = -1;
            var progress = new Progress<LibraryMoveProgress>(p =>
            {
                if (p.Percent == last) return;
                last = p.Percent;
                Tell($"Moving the library… {p.Percent} % ({SettingsVm.Size(p.DoneBytes)} of {SettingsVm.Size(p.TotalBytes)})", Tone.Info);
            });
            try
            {
                await _host.MoveLibraryAsync(plan.To, c =>
                {
                    if (c.Store.GetUnfinishedOperation() is not null)
                        throw new InvalidOperationException("A change to the game did not finish. Finish or undo it first, then move the library.");
                }, () => mover.Move(plan, progress));
            }
            catch (Exception ex) when (IsExpected(ex)) { failed = ex.Message; }
            LogPrivacy.LibraryRoot = AppSettings.ResolveLibrary(null);
            await RefreshAsync();
        });

        if (failed is not null) { Tell(failed, Tone.Block); return; }
        // the Settings overlay shows the old folder; close it and say where the library is now
        Overlay = null;
        var rest = Directory.Exists(plan.From) ? " The old folder still holds files AFTERLIFE did not put there or could not delete; nothing in it is needed any more." : "";
        Say($"Library moved to {plan.To}. Nothing changed in the game." + rest, Tone.Ok);
    }

    /// <summary>
    /// At start, before the library opens: a move that only missed its last step is finished; for a copy that
    /// stopped half-way the user chooses between continuing and keeping the library where it is.
    /// </summary>
    private async Task RecoverLibraryMoveAsync()
    {
        if (MoveBlockedByEnvironment() is not null) return;
        var mover = NewLibraryMover();
        try
        {
            switch (await Task.Run(() => mover.Inspect()))
            {
                case LibraryMovePending.Finish:
                    var to = await Task.Run(() => mover.FinishPending());
                    Say($"The library move was finished: the library is in {to}.", Tone.Ok);
                    break;
                case LibraryMovePending.Interrupted when mover.Pending() is { } state:
                    var ask = new ConfirmVm("THE LIBRARY MOVE DID NOT FINISH",
                        "AFTERLIFE was closed while it was copying the library to a new folder. The library in the old folder is complete, nothing is lost. "
                        + "Continue the move, or keep the library where it is? Keeping it deletes the unfinished copy in the new folder.",
                        "CONTINUE THE MOVE", lines: [$"Old folder: {state.From}", $"New folder: {state.To}"])
                    { CancelLabel = "Keep it where it is" };
                    if (await AskAsync(ask))
                    {
                        var last = -1;
                        var progress = new Progress<LibraryMoveProgress>(p =>
                        {
                            if (p.Percent / 5 == last) return;
                            last = p.Percent / 5;
                            Say($"Moving the library… {p.Percent} %", Tone.Info);
                        });
                        await Task.Run(() => mover.Continue(progress));
                        Say($"Library moved to {state.To}. Nothing changed in the game.", Tone.Ok);
                    }
                    else
                    {
                        await Task.Run(() => mover.Undo());
                        Say("The library stays where it was. The unfinished copy was deleted.", Tone.Info);
                    }
                    break;
            }
        }
        catch (Exception ex) when (IsExpected(ex)) { Say("The library move could not be finished: " + ex.Message, Tone.Block); }
    }
}
