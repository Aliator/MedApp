namespace MedApp.Application.Procedures.Common;

/// <summary>
/// Due dates are whole days: a procedure is due by the end of its day, UK time. They're stored as that moment in
/// UTC, so the day survives the clocks changing.
/// </summary>
public static class DueDates
{
    public static readonly TimeZoneInfo UkTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    /// <summary>The last moment of the day, UK time, in UTC.</summary>
    public static DateTime EndOfDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Unspecified), UkTimeZone);

    /// <summary>The first moment of the day, UK time, in UTC.</summary>
    public static DateTime StartOfDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), UkTimeZone);

    /// <summary>The UK day that a UTC moment falls on.</summary>
    public static DateOnly ToUkDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), UkTimeZone));
}
