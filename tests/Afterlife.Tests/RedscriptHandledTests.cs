using Afterlife.Core;

namespace Afterlife.Tests;

public class RedscriptHandledTests
{
    private static readonly DateTimeOffset Written = new(2026, 11, 23, 20, 14, 5, TimeSpan.Zero);

    [Fact]
    public void Nothing_stored_means_the_log_is_new()
    {
        Assert.True(RedscriptHandled.IsNew(Written, null));
        Assert.True(RedscriptHandled.IsNew(Written, ""));
        Assert.True(RedscriptHandled.IsNew(Written, "not a time"));
    }

    [Fact]
    public void The_same_log_is_not_new_again()
    {
        var stored = RedscriptHandled.Format(Written);
        Assert.False(RedscriptHandled.IsNew(Written, stored));
        Assert.False(RedscriptHandled.IsNew(Written.AddSeconds(-30), stored));
    }

    [Fact]
    public void A_log_written_later_is_new()
    {
        var stored = RedscriptHandled.Format(Written);
        Assert.True(RedscriptHandled.IsNew(Written.AddMilliseconds(1), stored));
        Assert.True(RedscriptHandled.IsNew(Written.AddDays(1), stored));
    }

    [Fact]
    public void Format_and_parse_round_trip_in_any_offset()
    {
        var local = Written.ToOffset(TimeSpan.FromHours(1));
        Assert.Equal<DateTimeOffset?>(Written, RedscriptHandled.Parse(RedscriptHandled.Format(local)));
        Assert.False(RedscriptHandled.IsNew(Written, RedscriptHandled.Format(local)));
    }

    [Fact]
    public void Negative_or_huge_values_are_ignored()
    {
        Assert.Null(RedscriptHandled.Parse("-5"));
        Assert.Null(RedscriptHandled.Parse("99999999999999999999"));
    }
}
