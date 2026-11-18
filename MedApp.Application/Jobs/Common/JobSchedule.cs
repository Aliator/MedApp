using System.Globalization;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Jobs.Common;

/// <summary>
/// When a scheduled job runs: every so often (<see cref="Every"/>), or once a day at a UK clock time
/// (<see cref="DailyAt"/>). A daily time must be outside 01:00 to 03:00, which the clock changes skip or repeat.
/// </summary>
public sealed class JobSchedule
{
    /// <summary>The UK clock times a daily job can't run at: the clock changes skip or repeat them.</summary>
    public static readonly TimeOnly ClockChangeStart = new(1, 0);

    public static readonly TimeOnly ClockChangeEnd = new(3, 0);

    /// <summary>The longest a failed run waits before it's tried again (sooner, for a job that runs more often).</summary>
    public static readonly TimeSpan LongestRetryDelay = TimeSpan.FromMinutes(30);

    private JobSchedule(TimeSpan interval, TimeOnly? dailyAt)
    {
        Interval = interval;
        DailyTime = dailyAt;
    }

    /// <summary>How long between runs: a day for a daily job.</summary>
    public TimeSpan Interval { get; }

    /// <summary>The UK clock time a daily job runs at; null for one that runs at an interval.</summary>
    public TimeOnly? DailyTime { get; }

    /// <summary>Every <paramref name="interval"/> (at least a minute), from the last run's start.</summary>
    public static JobSchedule Every(TimeSpan interval)
    {
        if (interval < TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(interval), "A job can't run more often than once a minute.");

        return new JobSchedule(interval, null);
    }

    /// <summary>Once a day at <paramref name="ukTime"/> (UK clock time), which must be outside 01:00 to 03:00.</summary>
    public static JobSchedule DailyAt(TimeOnly ukTime)
    {
        if (ukTime >= ClockChangeStart && ukTime < ClockChangeEnd)
            throw new ArgumentOutOfRangeException(nameof(ukTime), "A daily job can't run between 01:00 and 03:00, which the clock changes skip or repeat.");

        return new JobSchedule(TimeSpan.FromDays(1), ukTime);
    }

    /// <summary>
    /// When a job first runs, the first time a runner sees it: at once for an interval job, at the next daily time for
    /// a daily one.
    /// </summary>
    public DateTime FirstRun(DateTime nowUtc) => DailyTime is null ? nowUtc : NextAfter(nowUtc);

    /// <summary>The next run after <paramref name="utc"/>: an interval later, or the next daily time after it.</summary>
    public DateTime NextAfter(DateTime utc)
    {
        if (DailyTime is not { } time)
            return utc + Interval;

        var date = UkTime.DateOf(utc);
        var candidate = UkTime.ToUtc(date, time);
        return candidate > utc ? candidate : UkTime.ToUtc(date.AddDays(1), time);
    }

    /// <summary>
    /// When to run next after a run that started at <paramref name="startedUtc"/> and ended at
    /// <paramref name="finishedUtc"/>. After a failure, sooner: <see cref="RetryDelay"/> after it ended.
    /// </summary>
    public DateTime NextRunAfter(DateTime startedUtc, DateTime finishedUtc, bool failed)
    {
        if (failed)
            return finishedUtc + RetryDelay;

        var next = NextAfter(startedUtc);
        return next > finishedUtc ? next : NextAfter(finishedUtc);
    }

    /// <summary>How long a failed run waits to be tried again: the interval, or 30 minutes if that's sooner.</summary>
    public TimeSpan RetryDelay => Interval < LongestRetryDelay ? Interval : LongestRetryDelay;

    /// <summary>Whether a job due at <paramref name="nextRunUtc"/> is more than two intervals late at <paramref name="nowUtc"/>.</summary>
    public bool IsOverdue(DateTime nextRunUtc, DateTime nowUtc) => nowUtc - nextRunUtc > 2 * Interval;

    /// <summary>The schedule in words: "Daily at 04:30 (UK time)", or "Every 15 minutes".</summary>
    public string Describe()
    {
        if (DailyTime is { } time)
            return string.Create(CultureInfo.InvariantCulture, $"Daily at {time:HH:mm} (UK time)");

        if (Interval.TotalMinutes < 60 || Interval.TotalMinutes % 60 != 0)
            return Plural((int)Interval.TotalMinutes, "minute");

        return Interval.TotalHours % 24 == 0 ? Plural((int)Interval.TotalDays, "day") : Plural((int)Interval.TotalHours, "hour");
    }

    private static string Plural(int count, string unit) =>
        count == 1
            ? string.Create(CultureInfo.InvariantCulture, $"Every {unit}")
            : string.Create(CultureInfo.InvariantCulture, $"Every {count} {unit}s");
}
