namespace Afterlife.Infrastructure;

/// <summary>Where a file of a <see cref="NexusPageQueue"/> stands.</summary>
public enum PageQueueState { Waiting, PageOpened, Downloaded, Skipped }

/// <summary>One Nexus file in a <see cref="NexusPageQueue"/>.</summary>
public sealed class PageQueueItem
{
    internal PageQueueItem(string modId, string fileId, string name)
    {
        ModId = modId;
        FileId = fileId;
        Name = name;
    }

    public string ModId { get; }
    public string FileId { get; }
    public string Name { get; }
    public string Key => ModId + "/" + FileId;
    public PageQueueState State { get; internal set; }
    /// <summary>The file arrived but could not be added to the library; the item counts as skipped.</summary>
    public bool Failed { get; internal set; }
    public string PageUrl => NexusPageQueue.FilePage(ModId, FileId);
}

/// <summary>
/// Free Nexus accounts download only on the website: one file page is open at a time, the user clicks Mod Manager
/// Download there and the file comes back through nxm://. This is the pure bookkeeping of such a run; opening pages
/// and importing files is up to the caller. A file arriving out of turn (or while paused) is still taken.
/// </summary>
public sealed class NexusPageQueue
{
    private readonly List<PageQueueItem> _items = [];

    /// <summary>The files in the order their pages open; the same file twice is kept once.</summary>
    public NexusPageQueue(IEnumerable<(string ModId, string FileId, string Name)> files)
    {
        foreach (var (modId, fileId, name) in files)
            if (!_items.Any(i => i.ModId == modId && i.FileId == fileId)) _items.Add(new PageQueueItem(modId, fileId, name));
    }

    /// <summary>The files tab of a mod with the file picked, as Mod Manager Download needs it.</summary>
    public static string FilePage(string modId, string? fileId) =>
        $"https://www.nexusmods.com/{NxmLink.Cyberpunk}/mods/{modId}?tab=files" + (fileId is null ? "" : $"&file_id={fileId}&nmm=1");

    public IReadOnlyList<PageQueueItem> Items => _items;
    public int Total => _items.Count;
    public int Downloaded => _items.Count(i => i.State == PageQueueState.Downloaded);
    public int Skipped => _items.Count(i => i.State == PageQueueState.Skipped);
    public int Failed => _items.Count(i => i.Failed);

    /// <summary>The file whose page is open now, if any.</summary>
    public PageQueueItem? Current => _items.FirstOrDefault(i => i.State == PageQueueState.PageOpened);

    /// <summary>1-based place of the open page in the queue (for "3/12"); 0 when none is open.</summary>
    public int Position => Current is { } c ? _items.IndexOf(c) + 1 : 0;

    public bool IsPaused { get; private set; }

    /// <summary>Every file arrived or was skipped.</summary>
    public bool IsFinished => _items.All(i => i.State is PageQueueState.Downloaded or PageQueueState.Skipped);

    /// <summary>Opens the next waiting file (the open one, if a page is open already). Null when nothing waits.</summary>
    public PageQueueItem? OpenNext()
    {
        IsPaused = false;
        if (Current is { } open) return open;
        var next = _items.FirstOrDefault(i => i.State == PageQueueState.Waiting);
        if (next is not null) next.State = PageQueueState.PageOpened;
        return next;
    }

    /// <summary>True when this file is in the queue and has not arrived or been skipped yet.</summary>
    public bool Wants(string modId, string fileId) =>
        Find(modId, fileId) is { State: PageQueueState.Waiting or PageQueueState.PageOpened };

    /// <summary>
    /// A file came back: added to the library (downloaded) or not (skipped, failed). True when it was the open page,
    /// so the caller opens the next one.
    /// </summary>
    public bool Arrived(string modId, string fileId, bool added)
    {
        if (Find(modId, fileId) is not { State: PageQueueState.Waiting or PageQueueState.PageOpened } item) return false;
        var wasOpen = item.State == PageQueueState.PageOpened;
        item.State = added ? PageQueueState.Downloaded : PageQueueState.Skipped;
        item.Failed = !added;
        return wasOpen;
    }

    /// <summary>Gives up the open page; the caller opens the next one. Null when no page was open.</summary>
    public PageQueueItem? Skip()
    {
        if (Current is not { } c) return null;
        c.State = PageQueueState.Skipped;
        return c;
    }

    /// <summary>No further pages open; the open one goes back to waiting (its file is still taken if it comes).</summary>
    public void Pause()
    {
        IsPaused = true;
        if (Current is { } c) c.State = PageQueueState.Waiting;
    }

    /// <summary>Skipped and failed files wait again. Returns how many.</summary>
    public int RetrySkipped()
    {
        var n = 0;
        foreach (var i in _items.Where(i => i.State == PageQueueState.Skipped))
        {
            i.State = PageQueueState.Waiting;
            i.Failed = false;
            n++;
        }
        return n;
    }

    private PageQueueItem? Find(string modId, string fileId) => _items.FirstOrDefault(i => i.ModId == modId && i.FileId == fileId);
}
