using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Appointments.Common;

/// <summary>
/// An appointment's booked time, for validators: inside the working day (<see cref="WorkingHours"/>) and in the
/// future, as booking can't be after the event.
/// </summary>
public static class BookingTimes
{
    public const string InThePast = "Appointments can't be booked in the past.";

    /// <summary>Why an appointment can't be booked at this time, or null if it can.</summary>
    public static string? WhyNot(DateTime startUtc, DateTime endUtc, DateTime nowUtc)
    {
        var start = UkTime.Utc(startUtc);
        return WorkingHours.WhyOutside(start, UkTime.Utc(endUtc))
               ?? (start <= nowUtc ? InThePast : null);
    }
}
