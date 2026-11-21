namespace Afterlife.Infrastructure;

/// <summary>What Nexus Mods last said about the request quota of the API key, read from the headers of an answer.</summary>
public sealed record NexusQuota(int? HourlyRemaining, int? DailyRemaining, DateTimeOffset At);

/// <summary>
/// The request quota of Nexus Mods and what AFTERLIFE does about it: it estimates what the update settings cost,
/// keeps a reserve for downloads, and pauses its own checks until the quota is back instead of asking again.
/// The published limits are per API key, for every program that uses it.
/// </summary>
public static class NexusLimits
{
    /// <summary>Published by Nexus Mods (help article "I have reached a daily or hourly limit"); the server is the authority.</summary>
    public const int PerDay = 20_000, PerHour = 500;

    /// <summary>Requests left alone in the hour so downloads and manual actions still work after a check.</summary>
    public const int HourlyReserve = 50;

    /// <summary>A setting that spends more than this share of the daily quota on full reads gets a warning.</summary>
    public const int DailyWarning = PerDay / 10;

    private const string PausedKey = "nexus:paused-until";
    private const string FullRunKey = "nexus:fullrun";

    private static NexusQuota? _observed;

    /// <summary>The quota from the newest answer of Nexus Mods in this run, if it sent one.</summary>
    public static NexusQuota? Observed => _observed;

    public static void Observe(NexusQuota? quota)
    {
        if (quota is not null) _observed = quota;
    }

    /// <summary>Reads the remaining requests from the rate limit headers (x-rl-hourly-remaining, x-rl-daily-remaining).</summary>
    public static NexusQuota? Parse(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, DateTimeOffset now)
    {
        int? hourly = null, daily = null;
        foreach (var (name, values) in headers)
        {
            if (!int.TryParse(values.FirstOrDefault(), out var n) || n < 0) continue;
            if (name.Equals("x-rl-hourly-remaining", StringComparison.OrdinalIgnoreCase)) hourly = n;
            else if (name.Equals("x-rl-daily-remaining", StringComparison.OrdinalIgnoreCase)) daily = n;
        }
        return hourly is null && daily is null ? null : new NexusQuota(hourly, daily, now);
    }

    /// <summary>The reserve is reached: a check stops here although Nexus would still answer.</summary>
    public static bool ReserveReached(NexusQuota? q) =>
        q is not null && ((q.HourlyRemaining is { } h && h <= HourlyReserve) || (q.DailyRemaining is { } d && d <= HourlyReserve));

    /// <summary>Requests a day the automatic check costs: the hourly question about changed mods and the spread full reads.</summary>
    public static int RequestsPerDay(int linkedMods, int fullCheckDays, bool auto) =>
        (auto ? 24 : 0) + (fullCheckDays <= 0 ? 0 : (int)Math.Ceiling(linkedMods / (double)fullCheckDays));

    /// <summary>Null when the settings are fine; otherwise what is too low and where to change it.</summary>
    public static string? SettingsWarning(int linkedMods, int fullCheckDays, bool auto)
    {
        var perDay = RequestsPerDay(linkedMods, fullCheckDays, auto);
        if (perDay <= DailyWarning) return null;
        var days = (int)Math.Ceiling(linkedMods / (double)(DailyWarning - 24));
        return $"That is about {perDay:N0} requests a day for {linkedMods:N0} mods, a large part of what Nexus Mods allows ({PerDay:N0} a day for your key, for every program together). Use {Math.Max(days, 1)} days or more.";
    }

    /// <summary>The quota is back at the next full hour (hourly) or at midnight GMT (daily).</summary>
    public static DateTimeOffset ResumeAt(NexusQuota? q, DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        if (q?.DailyRemaining is { } d && d <= HourlyReserve) return new DateTimeOffset(utc.Date.AddDays(1), TimeSpan.Zero);
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero).AddHours(1).AddMinutes(1);
    }

    public static void Pause(AfterlifeStore store, DateTimeOffset until) =>
        store.SetMeta(PausedKey, until.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Until when the checks of AFTERLIFE rest; null when they may run.</summary>
    public static DateTimeOffset? PausedUntil(AfterlifeStore store, DateTimeOffset now) =>
        long.TryParse(store.GetMeta(PausedKey), out var t) && DateTimeOffset.FromUnixTimeSeconds(t) is var until && until > now ? until : null;

    /// <summary>"Check every mod" was asked for at this time; mods not read since are still owed.</summary>
    public static void StartFullRun(AfterlifeStore store, DateTimeOffset now) =>
        store.SetMeta(FullRunKey, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static DateTimeOffset? FullRunSince(AfterlifeStore store) =>
        long.TryParse(store.GetMeta(FullRunKey), out var t) && t > 0 ? DateTimeOffset.FromUnixTimeSeconds(t) : null;

    public static void EndFullRun(AfterlifeStore store) => store.SetMeta(FullRunKey, "0");
}
