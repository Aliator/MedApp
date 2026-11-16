using System.Text.RegularExpressions;
using MedApp.Domain.Appointments;
using static MedApp.Domain.Appointments.AppointmentStatus;

namespace MedApp.Application.Appointments.Common;

/// <summary>
/// The rules of an appointment's statuses.
/// <para>
/// Requested (needs booking) has no member of staff or time. Booking gives it both (in the future, clash-free), and a
/// Booked one can be moved the same way. Once its start has passed, it's marked Attended or Did not attend, which can
/// be corrected either way. Requested and Booked ones can be Cancelled, with a reason; Cancelled is final.
/// </para>
/// </summary>
public static partial class AppointmentWorkflow
{
    /// <summary>Still to happen: a stage can have one of these at a time.</summary>
    public static readonly IReadOnlyList<AppointmentStatus> OpenStatuses = [Requested, Booked];

    /// <summary>On a member of staff's calendar, taking up their time.</summary>
    public static readonly IReadOnlyList<AppointmentStatus> OnCalendarStatuses = [Booked, Attended, DidNotAttend];

    /// <summary>The outcomes that can be recorded.</summary>
    public static readonly IReadOnlyList<AppointmentStatus> Outcomes = [Attended, DidNotAttend];

    public static string OutcomeNames => string.Join(", ", Outcomes);

    public static bool IsOpen(AppointmentStatus status) => OpenStatuses.Contains(status);

    public static bool IsOnCalendar(AppointmentStatus status) => OnCalendarStatuses.Contains(status);

    public static bool IsOutcome(AppointmentStatus status) => Outcomes.Contains(status);

    /// <summary>A Requested one can be booked.</summary>
    public static bool CanBook(Appointment appointment) => appointment.Status == Requested;

    /// <summary>A Booked one can be moved to another time or member of staff.</summary>
    public static bool CanReschedule(Appointment appointment) => appointment.Status == Booked;

    /// <summary>Booked (or already marked) and started by <paramref name="nowUtc"/>.</summary>
    public static bool CanRecordOutcome(Appointment appointment, DateTime nowUtc) =>
        appointment.Status is Booked or Attended or DidNotAttend
        && appointment.StartUtc is { } start
        && start <= nowUtc;

    public static bool CanCancel(Appointment appointment) => IsOpen(appointment.Status);

    public static bool CanEditNotes(Appointment appointment) => appointment.Status != Cancelled;

    /// <summary>Why an outcome can't be recorded now, or null if it can.</summary>
    public static string? WhyOutcomeCantBeRecorded(Appointment appointment, DateTime nowUtc)
    {
        if (appointment.Status is not (Booked or Attended or DidNotAttend))
            return $"An appointment that's {Describe(appointment.Status)} has no outcome to record.";

        return CanRecordOutcome(appointment, nowUtc)
            ? null
            : "Whether the patient attended can be recorded once the appointment has started.";
    }

    /// <summary>A status in words, e.g. "did not attend".</summary>
    public static string Describe(AppointmentStatus status) =>
        WordBoundary().Replace(status.ToString(), " $1").ToLowerInvariant();

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
