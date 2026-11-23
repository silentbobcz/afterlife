using Afterlife.Infrastructure;

namespace Afterlife.Tests;

/// <summary>The bulk bar says how many checked mods the list does not show, and why.</summary>
public class CheckedRowsTests
{
    private sealed record Row(string Name, bool Checked, string Chapter = "MODS");

    private static readonly Row[] Rows =
    [
        new("Alpha", true),
        new("Beta", true),
        new("Gamma", false),
        new("Delta", true, "CATEGORY:weapons"),
    ];

    private static HiddenChecked Hidden(string search, Func<Row, bool>? filter = null, params string[] folded) =>
        CheckedRows.Hidden(Rows, r => r.Checked,
            r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) && (filter?.Invoke(r) ?? true),
            r => folded.Contains(r.Chapter));

    [Fact]
    public void Everything_in_view_counts_nothing()
    {
        var h = Hidden("");
        Assert.Equal(0, h.Total);
        Assert.Equal("", CheckedRows.Note(h));
        Assert.Null(CheckedRows.Tip(h, searching: false, filtering: false));
    }

    [Fact]
    public void Search_hides_checked_rows_but_not_unchecked_ones()
    {
        var h = Hidden("l"); // Alpha and Delta match; Beta is checked and hidden, Gamma is hidden but not checked
        Assert.Equal(new HiddenChecked(1, 0), h);
        Assert.Equal(" (1 not visible)", CheckedRows.Note(h));
        Assert.Equal("1 checked mod is not visible: 1 hidden by the search. The bulk actions still include them.",
            CheckedRows.Tip(h, searching: true, filtering: false));
    }

    [Fact]
    public void Filter_hides_checked_rows()
    {
        var h = Hidden("", r => r.Name != "Alpha" && r.Name != "Delta");
        Assert.Equal(new HiddenChecked(2, 0), h);
        Assert.Equal(" (2 not visible)", CheckedRows.Note(h));
        Assert.Contains("2 hidden by the filters", CheckedRows.Tip(h, searching: false, filtering: true));
        Assert.Contains("by the search and the filters", CheckedRows.Tip(h, searching: true, filtering: true));
    }

    [Fact]
    public void Folded_section_counts_once_and_filter_wins()
    {
        // Delta sits in a folded category; Alpha is filtered out and in a folded MODS, but counts only as filtered
        var h = Hidden("", r => r.Name != "Alpha", "CATEGORY:weapons", "MODS");
        Assert.Equal(new HiddenChecked(1, 2), h);
        Assert.Equal(3, h.Total);
        Assert.Equal("3 checked mods are not visible: 1 hidden by the filters, 2 in folded sections of the list. The bulk actions still include them.",
            CheckedRows.Tip(h, searching: false, filtering: true));
    }
}
