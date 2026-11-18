using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Appointments.Common;

/// <summary>
/// An appointment's time, for validators: inside the member of staff's working day (<see cref="WorkingDays"/>: one of
/// their days, their hours, not a closed day), and in the future when it's booked; or, when it's recorded after the
/// event, started within the last <see cref="RecordAfterEventDays"/> days, inside their working day, closed or not.
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

    /// <summary>
    /// Why an appointment can't be booked with this member of staff at this time, or null if it can: inside their
    /// working day (<see cref="WorkingDays"/>: one of their days, their hours, not closed), and in the future.
    /// </summary>
    public static async Task<string?> WhyNotAsync(
        WorkingDays workingDays, Guid staffUserId, DateTime startUtc, DateTime endUtc, DateTime nowUtc, CancellationToken ct)
    {
        var start = UkTime.Utc(startUtc);
        var why = staffUserId == Guid.Empty
            ? await workingDays.WhyOutsideAsync(start, UkTime.Utc(endUtc), ct)
            : await workingDays.WhyOutsideAsync(staffUserId, start, UkTime.Utc(endUtc), ct);
        return why ?? (start <= nowUtc ? InThePast : null);
    }

    /// <summary>
    /// Why an appointment can't be recorded after the event at this time, or null if it can: its start passed (as an
    /// outcome is recorded once the start has passed), not before the UK day <see cref="RecordAfterEventDays"/> days ago,
    /// and inside the member of staff's working day that date (one of their days, their hours). **A closed day doesn't
    /// count** (decision 74): a visit that happened on a day closed later, or part-way through, is recorded as it
    /// happened, and shows flagged.
    /// </summary>
    public static async Task<string?> WhyNotRecordedAsync(
        WorkingDays workingDays, Guid staffUserId, DateTime startUtc, DateTime endUtc, DateTime nowUtc, CancellationToken ct)
    {
        var start = UkTime.Utc(startUtc);
        if (WorkingHours.WhyBadShape(start, UkTime.Utc(endUtc)) is { } shape)
            return shape;

        if (start > nowUtc)
            return NotStartedYet;

        if (UkTime.DateOf(start) < UkTime.DateOf(nowUtc).AddDays(-RecordAfterEventDays))
            return TooLongAgo;

        return staffUserId == Guid.Empty
            ? null
            : await workingDays.WhyOutsideAsync(staffUserId, start, UkTime.Utc(endUtc), ct, closedDaysCount: false);
    }
}
