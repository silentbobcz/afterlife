using Afterlife.Infrastructure;

namespace Afterlife.Tests;

public sealed class LinkInboxTests : IDisposable
{
    private const string Mod = "nxm://cyberpunk2077/mods/107/files/12345?key=abc&expires=1700000000&user_id=9";
    private const string Collection = "nxm://cyberpunk2077/collections/g14kxi/revisions/2";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "afterlife-inbox-" + Guid.NewGuid().ToString("N"));
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(10);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Theory]
    [InlineData(Mod, true)]
    [InlineData(Collection, true)]
    [InlineData("activate", true)]
    [InlineData("  ACTIVATE ", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("https://www.nexusmods.com/cyberpunk2077/mods/107", false)]
    [InlineData("nxm://cyberpunk2077/mods/abc/files/1", false)]
    [InlineData("C:\\Windows\\System32\\cmd.exe", false)]
    [InlineData("activate now", false)]
    public void Accepts_only_nxm_links_and_activate(string? text, bool expected) =>
        Assert.Equal(expected, LinkInbox.IsAccepted(text));

    [Fact]
    public void Dropped_link_is_read_once_and_the_file_is_gone()
    {
        var inbox = new LinkInbox(_folder);
        var file = inbox.Drop(Mod);
        Assert.NotNull(file);
        Assert.True(File.Exists(file));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));

        Assert.Equal(new[] { Mod }, inbox.Drain(Fresh));
        Assert.False(File.Exists(file));
        Assert.Empty(inbox.Drain(Fresh));
    }

    [Fact]
    public void Refused_text_is_never_written()
    {
        var inbox = new LinkInbox(_folder);
        Assert.Null(inbox.Drop("https://example.com/evil"));
        Assert.False(Directory.Exists(_folder) && Directory.GetFiles(_folder).Length > 0);
    }

    [Fact]
    public void Foreign_file_in_the_folder_is_deleted_and_not_returned()
    {
        Directory.CreateDirectory(_folder);
        var foreign = Path.Combine(_folder, "0000000000000000001-x.link");
        File.WriteAllText(foreign, "cmd /c whatever");
        var inbox = new LinkInbox(_folder);
        Assert.Empty(inbox.Drain(Fresh));
        Assert.False(File.Exists(foreign));
    }

    [Fact]
    public void Several_links_come_out_in_the_order_they_were_dropped()
    {
        var inbox = new LinkInbox(_folder);
        var links = Enumerable.Range(1, 5).Select(i => $"nxm://cyberpunk2077/mods/{i}/files/{i * 10}").ToList();
        foreach (var l in links) Assert.NotNull(inbox.Drop(l));
        Assert.Equal(links, inbox.Drain(Fresh));
    }

    [Fact]
    public void Old_link_left_behind_is_dropped_not_downloaded()
    {
        var inbox = new LinkInbox(_folder);
        var file = inbox.Drop(Mod)!;
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-1));
        Assert.Empty(inbox.Drain(Fresh));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Unfinished_temporary_file_is_left_alone_while_new()
    {
        Directory.CreateDirectory(_folder);
        var tmp = Path.Combine(_folder, "0000000000000000001-x.tmp");
        File.WriteAllText(tmp, Mod);
        var inbox = new LinkInbox(_folder);
        Assert.Empty(inbox.Drain(Fresh));
        Assert.True(File.Exists(tmp));
    }
}
