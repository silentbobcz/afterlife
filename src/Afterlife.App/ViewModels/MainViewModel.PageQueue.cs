using System.IO;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>
/// Free Nexus accounts download only on the website: AFTERLIFE opens one file page after another and takes the file
/// that comes back through Mod Manager Download (nxm://, <see cref="ImportDownloadedAsync"/>). Collections and the
/// missing mods of a load list both lead the user this way and show it in the collection download view
/// (<see cref="CollectionProgressVm"/>). The bookkeeping of the load list run is <see cref="NexusPageQueue"/>.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Marks a row as open on Nexus and opens its file page; the next one opens when the file has arrived.</summary>
    private static void OpenFilePage(CollectionProgressVm progress, string key, string name, string url)
    {
        if (progress.Item(key) is { } row) { row.Status = "open on Nexus"; row.Tone = Tone.Info; }
        progress.Current = name;
        progress.CurrentPercent = 0;
        progress.StatusText = $"Click Mod Manager Download on the page for {name}. AFTERLIFE opens the next one by itself.";
        AppLinks.Open(url);
    }

    /// <summary>The missing mods of a load list being fetched page by page, the list to load again, and its window.</summary>
    private NexusPageQueue? _listQueue;
    private string? _listQueuePath;
    private LoadListQueueVm? _listQueueWindow;

    /// <summary>A page of the load list queue is open (not paused, not finished): IMPORT ORDER shows the queue instead.</summary>
    private bool ListQueueRunning => _listQueue is { IsPaused: false, Current: not null };

    /// <summary>Free account: the missing mods that name a Nexus file, one file page after another; they come in switched off.</summary>
    private void StartListQueue(IReadOnlyList<LoadListMod> missing, string path)
    {
        if (LockedOffline("Getting mods from Nexus Mods")) return;
        if (!SafeNxmRegistered())
        {
            Say("Nexus lets free accounts download only on the website. Turn on Mod Manager Download links in Settings first; then Get from Nexus opens the pages of the missing mods one after another and the files come straight into AFTERLIFE.", Tone.Warn);
            OpenSettings();
            return;
        }
        var queue = new NexusPageQueue(missing.Where(m => m.NexusModId is not null && m.NexusFileId is not null)
            .Select(m => (m.NexusModId!, m.NexusFileId!, m.Name + (m.Version is null ? "" : " " + m.Version))));
        if (queue.Total == 0) { Say("None of the missing mods names a Nexus file. Open their pages from IMPORT ORDER.", Tone.Warn); return; }

        var progress = new CollectionProgressVm("load-list", Path.GetFileName(path))
        {
            PauseCommand = new RelayCommand(PauseListQueue),
            ResumeCommand = new RelayCommand(ResumeListQueue),
        };
        foreach (var i in queue.Items) progress.Items.Add(new CollectionProgressItemVm(i.Key, i.Name));
        _listQueue = queue;
        _listQueuePath = path;
        _listQueueWindow = new LoadListQueueVm(Path.GetFileName(path), progress,
            new RelayCommand(SkipListPage), new AsyncRelayCommand(LoadListAgainAsync))
        {
            CloseCommand = new RelayCommand(() => Overlay = null), // the queue goes on; IMPORT ORDER shows it again
        };
        Overlay = _listQueueWindow;
        OpenNextListPage();
    }

    /// <summary>Rows, counts and buttons from the queue.</summary>
    private void SyncListQueue()
    {
        if (_listQueue is not { } q || _listQueueWindow is not { } w) return;
        var p = w.Progress;
        foreach (var i in q.Items)
        {
            if (p.Item(i.Key) is not { } row) continue;
            (row.Status, row.Tone) = i.State switch
            {
                PageQueueState.PageOpened => ("open on Nexus", Tone.Info),
                PageQueueState.Downloaded => ("in library", Tone.Ok),
                PageQueueState.Skipped => i.Failed ? ("not added", Tone.Block) : ("skipped", Tone.Warn),
                _ => ("waiting", Tone.Neutral),
            };
        }
        p.Total = q.Total;
        p.Done = q.Downloaded;
        p.Failed = q.Failed;
        p.IsRunning = q.Current is not null;
        p.IsPaused = q.IsPaused && !q.IsFinished;
        w.CanSkip = q.Current is not null;
        w.CanLoadAgain = q.Current is null && q.Downloaded > 0;
    }

    private void OpenNextListPage()
    {
        if (_listQueue is not { } q || _listQueueWindow is not { } w) return;
        var next = q.OpenNext();
        SyncListQueue();
        if (next is null) { FinishListQueue(); return; }
        OpenFilePage(w.Progress, next.Key, next.Name, next.PageUrl);
        Say($"Missing mods: click Mod Manager Download on the page ({q.Position}/{q.Total}: {next.Name}). AFTERLIFE opens the next one by itself.", Tone.Info);
    }

    private void FinishListQueue()
    {
        if (_listQueue is not { } q || _listQueueWindow is not { } w) return;
        var p = w.Progress;
        p.Current = null;
        p.CurrentPercent = 0;
        var rest = q.Total - q.Downloaded;
        p.StatusText = q.Downloaded == 0
            ? "No file came in. Resume opens the skipped pages again."
            : $"{q.Downloaded} of {q.Total} mod(s) are in the library, switched off. Load the list again to put them in place."
              + (rest > 0 ? $" Resume opens the {rest} skipped page(s) again." : "");
        Say(q.Downloaded == 0 ? "Missing mods: no file came in from Nexus."
            : $"Missing mods: {q.Downloaded} of {q.Total} in the library. Load the list again to put them in place.", q.Downloaded == q.Total ? Tone.Ok : Tone.Warn);
        if (Overlay is null) Overlay = w;
    }

    private void SkipListPage()
    {
        if (_listQueue is not { } q || q.Skip() is null) return;
        OpenNextListPage();
    }

    private void PauseListQueue()
    {
        if (_listQueue is not { } q || _listQueueWindow is not { } w) return;
        q.Pause();
        SyncListQueue();
        w.Progress.Current = null;
        w.Progress.StatusText = "Paused. A file from the page already open still comes in. Resume opens the next page.";
    }

    private void ResumeListQueue()
    {
        if (_listQueue is not { } q) return;
        if (q.IsFinished) q.RetrySkipped();
        OpenNextListPage();
    }

    /// <summary>True when the arriving file is one the load list queue waits for: it comes in switched off and quietly.</summary>
    private bool ListQueueWants(NxmLink link) => _listQueue?.Wants(link.ModId, link.FileId) == true;

    /// <summary>A file of the load list queue came in (or failed to); the next page opens when it was the open one.</summary>
    private void ListFileArrived(NxmLink link, bool added)
    {
        if (_listQueue is not { } q) return;
        var wasOpen = q.Arrived(link.ModId, link.FileId, added);
        SyncListQueue();
        if (q.IsFinished) { FinishListQueue(); return; }
        if (wasOpen && !q.IsPaused) OpenNextListPage();
    }

    /// <summary>The offer after the queue: the same list again, now with the mods that came in.</summary>
    private Task LoadListAgainAsync() => RunUiAsync(async () =>
    {
        var path = _listQueuePath;
        ClearListQueue();
        Overlay = null;
        if (path is not null) await ImportLoadListAsync(path);
    });

    private void ClearListQueue()
    {
        _listQueue = null;
        _listQueuePath = null;
        _listQueueWindow = null;
    }

    /// <summary>IMPORT ORDER while a page of the queue is open shows the queue window again.</summary>
    private bool ShowListQueue()
    {
        if (!ListQueueRunning || _listQueueWindow is not { } w) return false;
        Overlay = w;
        return true;
    }
}
