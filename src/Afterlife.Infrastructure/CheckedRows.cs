namespace Afterlife.Infrastructure;

/// <summary>Checked rows the mod list does not show: hidden by the search or a filter, or inside a folded section.</summary>
public readonly record struct HiddenChecked(int ByFilter, int Folded)
{
    public int Total => ByFilter + Folded;
}

/// <summary>
/// Checked mods stay checked when the list hides them, and the bulk actions still take them. These helpers count
/// them and explain why, so the bulk bar can say so. Pure, so it can be tested without the window.
/// </summary>
public static class CheckedRows
{
    /// <summary>A checked row that fails the search or a filter counts as filtered; otherwise one in a folded section counts as folded.</summary>
    public static HiddenChecked Hidden<T>(IEnumerable<T> rows, Func<T, bool> isChecked, Func<T, bool> shownByFilter, Func<T, bool> inFoldedSection)
    {
        int byFilter = 0, folded = 0;
        foreach (var r in rows)
        {
            if (!isChecked(r)) continue;
            if (!shownByFilter(r)) byFilter++;
            else if (inFoldedSection(r)) folded++;
        }
        return new HiddenChecked(byFilter, folded);
    }

    /// <summary>" (2 not visible)" after the checked count, or "" when every checked mod is in view.</summary>
    public static string Note(HiddenChecked h) => h.Total == 0 ? "" : $" ({h.Total} not visible)";

    /// <summary>Why the checked mods are not visible; null when all of them are.</summary>
    public static string? Tip(HiddenChecked h, bool searching, bool filtering)
    {
        if (h.Total == 0) return null;
        var parts = new List<string>();
        if (h.ByFilter > 0)
        {
            var by = searching && filtering ? "the search and the filters" : searching ? "the search" : "the filters";
            parts.Add($"{h.ByFilter} hidden by {by}");
        }
        if (h.Folded > 0) parts.Add($"{h.Folded} in folded sections of the list");
        var what = h.Total == 1 ? "1 checked mod is not visible" : $"{h.Total} checked mods are not visible";
        return $"{what}: {string.Join(", ", parts)}. The bulk actions still include them.";
    }
}
