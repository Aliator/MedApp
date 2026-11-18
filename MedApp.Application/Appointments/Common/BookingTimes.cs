using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Appointments.Common;

/// <summary>
/// An appointment's time, for validators: inside the working day (<see cref="WorkingDays"/>: not a weekend or a closed
/// day), and in the future when it's booked; or, when it's recorded after the event, started within the last
/// <see cref="RecordAfterEventDays"/> days on a weekday, closed or not.
/// </summary>
public static class BookingTimes
{
    public const string InThePast = "Appointments can't be booked in the past.";

    /// <summary>How far back an appointment can be recorded after the event, in UK days.</summary>
    public const int RecordAfterEventDays = 30;

    public const string NotStartedYet =
        "Only an appointment that has already started can be recorded after the event. Book it instead.";

    public static readonly string TooLongAgo =
        $"Appointments can be recorded after the event for up to {RecordAfterEventDays} days.";

    /// <summary>Why an appointment can't be booked at this time, or null if it can.</summary>
    public static async Task<string?> WhyNotAsync(
        WorkingDays workingDays, DateTime startUtc, DateTime endUtc, DateTime nowUtc, CancellationToken ct)
    {
        var start = UkTime.Utc(startUtc);
        return await workingDays.WhyOutsideAsync(start, UkTime.Utc(endUtc), ct)
               ?? (start <= nowUtc ? InThePast : null);
    }

    /// <summary>
    /// Why an appointment can't be recorded after the event at this time, or null if it can: on a weekday, inside the
    /// working day, its start passed (as an outcome is recorded once the start has passed), and not before the UK day
    /// <see cref="RecordAfterEventDays"/> days ago. **A closed day doesn't count** (decision 74): a visit that happened
    /// on a day closed later, or part-way through, is recorded as it happened, and shows flagged.
    /// </summary>
    public static string? WhyNotRecorded(DateTime startUtc, DateTime endUtc, DateTime nowUtc)
    {
        var start = UkTime.Utc(startUtc);
        if (WorkingHours.WhyOutside(start, UkTime.Utc(endUtc), ClosedDaySet.None) is { } why)
            return why;

        if (start > nowUtc)
            return NotStartedYet;

        return UkTime.DateOf(start) < UkTime.DateOf(nowUtc).AddDays(-RecordAfterEventDays) ? TooLongAgo : null;
    }
}
