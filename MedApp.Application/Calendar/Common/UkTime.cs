using System.Globalization;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// UK dates and clock times (Europe/London) to and from UTC. Calendars are worked in UK time, but times are stored and
/// sent as UTC, so a 12:00 lunch is at 12:00 UK time on both sides of a clock change.
/// </summary>
public static class UkTime
{
    /// <summary>A UK clock time on a UK date, in UTC.</summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), DueDates.UkTimeZone);

    /// <summary>A UTC moment as UK local time.</summary>
    public static DateTime ToUk(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), DueDates.UkTimeZone);

    /// <summary>The UK date a UTC moment falls on.</summary>
    public static DateOnly DateOf(DateTime utc) => DateOnly.FromDateTime(ToUk(utc));

    /// <summary>The UK clock time of a UTC moment.</summary>
    public static TimeOnly TimeOf(DateTime utc) => TimeOnly.FromDateTime(ToUk(utc));

    /// <summary>Today's date in the UK.</summary>
    public static DateOnly Today(TimeProvider timeProvider) => DateOf(timeProvider.GetUtcNow().UtcDateTime);

    /// <summary>A moment in words, UK time, for messages: "Monday 23 November at 10:00".</summary>
    public static string Describe(DateTime utc) =>
        ToUk(utc).ToString("dddd d MMMM 'at' HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A moment's UK clock time, for messages: "10:00".</summary>
    public static string DescribeTime(DateTime utc) => ToUk(utc).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A time on one day in words, UK time, for messages: "Monday 23 November, 10:00 to 11:00".</summary>
    public static string DescribeRange(DateTime startUtc, DateTime endUtc) =>
        $"{ToUk(startUtc).ToString("dddd d MMMM, HH:mm", CultureInfo.InvariantCulture)} to {DescribeTime(endUtc)}";

    /// <summary>A UK date in words, for messages: "Monday 23 November".</summary>
    public static string DescribeDate(DateOnly date) => date.ToString("dddd d MMMM", CultureInfo.InvariantCulture);

    /// <summary>
    /// A time from a request, as UTC. JSON with a <c>Z</c> or an offset is read as UTC or local time; one without is
    /// taken to be UTC already, as the API sends every time with a <c>Z</c>.
    /// </summary>
    public static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
