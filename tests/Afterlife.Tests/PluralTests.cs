using Afterlife.Core;

namespace Afterlife.Tests;

public class PluralTests
{
    [Theory]
    [InlineData(0, "0 mods")]
    [InlineData(1, "1 mod")]
    [InlineData(2, "2 mods")]
    [InlineData(300, "300 mods")]
    public void Of_picks_the_form_by_count(int count, string expected) =>
        Assert.Equal(expected, Plural.Of(count, "mod"));

    [Fact]
    public void Of_uses_the_given_plural()
    {
        Assert.Equal("1 copy", Plural.Of(1, "copy", "copies"));
        Assert.Equal("3 copies", Plural.Of(3, "copy", "copies"));
    }

    [Fact]
    public void Word_has_no_number()
    {
        Assert.Equal("file", Plural.Word(1, "file"));
        Assert.Equal("files", Plural.Word(0, "file"));
    }

    [Fact]
    public void Long_counts_work()
    {
        Assert.Equal("1 file", Plural.Of(1L, "file"));
        Assert.Equal("5000000000 files", Plural.Of(5_000_000_000L, "file"));
    }
}
