using MedApp.Application.Calendar.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Appointments.Common;

/// <summary>
/// What to tell members of staff when someone else changes their appointments: booked with them (or moved to them),
/// moved to another time, or cancelled (or moved to someone else). Nobody is told about their own change. A
/// notification's title is the procedure's title, or "Appointment"; its message says who and when, never the patient's
/// name or notes.
/// </summary>
public static class AppointmentNotifications
{
    public const string DefaultTitle = "Appointment";

    /// <summary>
    /// After booking or moving an appointment, given where it was before (its status, member of staff and start).
    /// </summary>
    public static IReadOnlyList<Notification> AfterBooking(
        Appointment appointment,
        string? procedureTitle,
        AppointmentStatus previousStatus,
        Guid? previousStaffUserId,
        DateTime? previousStartUtc,
        ProcedureActor actor,
        DateTime nowUtc)
    {
        var notifications = new List<Notification>();
        if (appointment is not { StaffUserId: { } staffUserId, StartUtc: { } startUtc })
            return notifications;

        var when = UkTime.Describe(startUtc);

        if (previousStatus != AppointmentStatus.Booked)
        {
            Add(NotificationKind.AppointmentBooked, staffUserId, $"{actor.Username} booked you an appointment on {when}.");
        }
        else if (previousStaffUserId == staffUserId)
        {
            if (previousStartUtc != startUtc)
                Add(NotificationKind.AppointmentRescheduled, staffUserId, $"{actor.Username} moved your appointment to {when}.");
        }
        else
        {
            Add(NotificationKind.AppointmentBooked, staffUserId, $"{actor.Username} moved an appointment to you, on {when}.");

            if (previousStaffUserId is { } previousStaff && previousStartUtc is { } previousStart)
                Add(
                    NotificationKind.AppointmentCancelled,
                    previousStaff,
                    $"{actor.Username} moved your appointment on {UkTime.Describe(previousStart)} to someone else.");
        }

        return notifications;

        void Add(NotificationKind kind, Guid userId, string message)
        {
            if (userId != actor.UserId)
                notifications.Add(Create(kind, userId, appointment, procedureTitle, message, nowUtc));
        }
    }

    /// <summary>After cancelling an appointment that was booked with someone else.</summary>
    public static IReadOnlyList<Notification> AfterCancelling(
        Appointment appointment,
        string? procedureTitle,
        AppointmentStatus previousStatus,
        ProcedureActor actor,
        DateTime nowUtc) =>
        previousStatus == AppointmentStatus.Booked
        && appointment is { StaffUserId: { } staffUserId, StartUtc: { } startUtc }
        && staffUserId != actor.UserId
            ? [Create(
                NotificationKind.AppointmentCancelled,
                staffUserId,
                appointment,
                procedureTitle,
                $"{actor.Username} cancelled your appointment on {UkTime.Describe(startUtc)}.",
                nowUtc)]
            : [];

    private static Notification Create(
        NotificationKind kind,
        Guid userId,
        Appointment appointment,
        string? procedureTitle,
        string message,
        DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Kind = kind,
        Title = string.IsNullOrWhiteSpace(procedureTitle) ? DefaultTitle : procedureTitle,
        Message = message,
        ProcedureId = appointment.ProcedureId,
        AppointmentId = appointment.Id,
        CreatedAtUtc = nowUtc
    };
}
