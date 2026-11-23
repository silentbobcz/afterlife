using System.Globalization;

namespace Afterlife.Core;

/// <summary>
/// Which redscript log the user has already seen: the last write time of the log, kept in the library's meta table,
/// so the notice about a failed compilation does not come back for the same log, also after a restart.
/// </summary>
public static class RedscriptHandled
{
    public const string MetaKey = "redscript.handled";

    /// <summary>The value stored for a log written at <paramref name="written"/>.</summary>
    public static string Format(DateTimeOffset written) => written.UtcTicks.ToString(CultureInfo.InvariantCulture);

    /// <summary>The stored write time; null when nothing (or nothing readable) is stored.</summary>
    public static DateTimeOffset? Parse(string? stored) =>
        long.TryParse(stored, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
        && ticks >= DateTimeOffset.MinValue.UtcTicks && ticks <= DateTimeOffset.MaxValue.UtcTicks
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;

    /// <summary>The log written at <paramref name="written"/> is newer than the one handled last.</summary>
    public static bool IsNew(DateTimeOffset written, string? stored) =>
        Parse(stored) is not { } handled || written.UtcTicks > handled.UtcTicks;
}
