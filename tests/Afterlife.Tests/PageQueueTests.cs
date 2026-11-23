using Afterlife.Infrastructure;

namespace Afterlife.Tests;

public class PageQueueTests
{
    private static NexusPageQueue Three() => new([("101", "11", "Mod A"), ("202", "22", "Mod B"), ("303", "33", "Mod C")]);

    private static PageQueueState[] States(NexusPageQueue q) => q.Items.Select(i => i.State).ToArray();

    [Fact]
    public void Opens_one_page_at_a_time_and_moves_on_when_its_file_arrives()
    {
        var q = Three();
        Assert.All(q.Items, i => Assert.Equal(PageQueueState.Waiting, i.State));
        var first = q.OpenNext();
        Assert.Equal("Mod A", first?.Name);
        Assert.Equal(1, q.Position);
        Assert.Same(first, q.OpenNext()); // a page is open: no second one
        Assert.True(q.Arrived("101", "11", added: true));
        Assert.Null(q.Current);
        Assert.Equal("Mod B", q.OpenNext()?.Name);
        Assert.Equal(2, q.Position);
        Assert.Equal(new[] { PageQueueState.Downloaded, PageQueueState.PageOpened, PageQueueState.Waiting }, States(q));
        Assert.Equal(1, q.Downloaded);
        Assert.False(q.IsFinished);
    }

    [Fact]
    public void Skip_gives_up_the_open_page_and_the_queue_finishes()
    {
        var q = Three();
        q.OpenNext();
        Assert.Equal("Mod A", q.Skip()?.Name);
        q.OpenNext();
        q.Arrived("202", "22", added: true);
        q.OpenNext();
        q.Arrived("303", "33", added: true);
        Assert.Null(q.OpenNext());
        Assert.True(q.IsFinished);
        Assert.Equal(2, q.Downloaded);
        Assert.Equal(1, q.Skipped);
        Assert.Equal(0, q.Failed);
        Assert.Null(q.Skip()); // nothing open
    }

    [Fact]
    public void A_file_arriving_out_of_turn_is_taken_and_the_open_page_stays()
    {
        var q = Three();
        q.OpenNext();
        Assert.True(q.Wants("303", "33"));
        Assert.False(q.Arrived("303", "33", added: true)); // not the open page: nothing new opens
        Assert.Equal("Mod A", q.Current?.Name);
        Assert.Equal(new[] { PageQueueState.PageOpened, PageQueueState.Waiting, PageQueueState.Downloaded }, States(q));
        Assert.False(q.Wants("303", "33")); // already in
    }

    [Fact]
    public void Only_the_exact_nexus_file_is_taken()
    {
        var q = Three();
        q.OpenNext();
        Assert.False(q.Wants("101", "12")); // another file of the same mod
        Assert.False(q.Wants("999", "11"));
        Assert.False(q.Arrived("101", "12", added: true));
        Assert.Equal(PageQueueState.PageOpened, q.Items[0].State);
    }

    [Fact]
    public void Pause_puts_the_open_page_back_and_its_file_is_still_taken()
    {
        var q = Three();
        q.OpenNext();
        q.Pause();
        Assert.True(q.IsPaused);
        Assert.Null(q.Current);
        Assert.Equal(PageQueueState.Waiting, q.Items[0].State);
        Assert.True(q.Wants("101", "11"));
        Assert.False(q.Arrived("101", "11", added: true));
        Assert.Equal(PageQueueState.Downloaded, q.Items[0].State);
        Assert.Equal("Mod B", q.OpenNext()?.Name); // resume
        Assert.False(q.IsPaused);
    }

    [Fact]
    public void A_file_that_could_not_be_added_counts_as_skipped_and_can_be_tried_again()
    {
        var q = Three();
        q.OpenNext();
        Assert.True(q.Arrived("101", "11", added: false));
        Assert.Equal(PageQueueState.Skipped, q.Items[0].State);
        Assert.True(q.Items[0].Failed);
        Assert.Equal(1, q.Failed);
        q.OpenNext(); q.Skip();
        q.OpenNext(); q.Arrived("303", "33", added: true);
        Assert.True(q.IsFinished);
        Assert.Equal(2, q.RetrySkipped());
        Assert.False(q.IsFinished);
        Assert.Equal(0, q.Failed);
        Assert.Equal("Mod A", q.OpenNext()?.Name);
    }

    [Fact]
    public void The_same_file_twice_is_kept_once_and_an_empty_queue_is_finished()
    {
        var q = new NexusPageQueue([("101", "11", "Mod A"), ("101", "11", "Mod A again"), ("101", "12", "Mod A optional")]);
        Assert.Equal(2, q.Total);
        Assert.Equal("101/11", q.Items[0].Key);
        var empty = new NexusPageQueue([]);
        Assert.True(empty.IsFinished);
        Assert.Null(empty.OpenNext());
    }

    [Fact]
    public void File_page_picks_the_file_for_mod_manager_download()
    {
        Assert.Equal("https://www.nexusmods.com/cyberpunk2077/mods/101?tab=files&file_id=11&nmm=1", NexusPageQueue.FilePage("101", "11"));
        Assert.Equal("https://www.nexusmods.com/cyberpunk2077/mods/101?tab=files", NexusPageQueue.FilePage("101", null));
        Assert.Equal(NexusPageQueue.FilePage("101", "11"), Three().Items[0].PageUrl);
    }
}
