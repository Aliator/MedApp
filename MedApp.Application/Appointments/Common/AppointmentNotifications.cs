using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Appointments.Common;

/// <summary>
/// What to tell members of staff when someone else changes their appointments: booked with them (or moved to them),
/// moved to another time, cancelled (or moved to someone else), or recorded after the event. Nobody is told about their own change. A
/// notification's title is a fixed phrase for its kind (<see cref="TitleOf"/>), so no free text ever reaches it; its
/// message says who and when, never the patient's name, the pathway's title or the notes.
/// </summary>
public static class AppointmentNotifications
{
    /// <summary>A fixed title for each appointment kind, so anonymising a patient leaves it as it is.</summary>
    public static string TitleOf(NotificationKind kind) => kind switch
    {
        NotificationKind.AppointmentBooked => "Appointment booked",
        NotificationKind.AppointmentRescheduled => "Appointment moved",
        NotificationKind.AppointmentCancelled => "Appointment cancelled",
        NotificationKind.AppointmentRecorded => "Appointment recorded",
        NotificationKind.StageVisitWithdrawn => "Stage visit withdrawn",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>
    /// After a walk-in was marked entered in error that was the only attended visit of a finished stage that requires
    /// one (<c>StageVisits.AfterVisitWithdrawn</c>): the pathway's assignees and every admin (<paramref name="adminIds"/>),
    /// but not whoever withdrew it, are told, since only an admin can unlock the stage. A fixed title, and a message of
    /// who, the stage's (admin-written) name and dates, never the patient, the pathway's title or a reason. Links to
    /// the pathway, not the appointment, which is off the calendar.
    /// </summary>
    public static IReadOnlyList<Notification> AfterStageVisitWithdrawn(
        Appointment withdrawn,
        PatientPathway pathway,
        PatientPathwayStage stage,
        IEnumerable<Guid> adminIds,
        PathwayActor actor,
        DateTime nowUtc)
    {
        var visit = withdrawn.StartUtc is { } start ? $"the visit on {UkTime.Describe(start)}" : "a visit";
        var finished = stage.CompletedAtUtc is { } completed ? $" on {UkTime.DescribeDate(UkTime.DateOf(completed))}" : "";
        var message =
            $"{actor.Username} marked {visit} for the '{stage.Name}' stage entered in error. " +
            $"The stage was finished{finished} and now has no attended visit, though it requires one. " +
            "If it still needs its visit, an admin can unlock it, and then book one.";

        return pathway.Assignments.Select(a => a.UserId)
            .Concat(adminIds)
            .Where(userId => userId != actor.UserId)
            .Distinct()
            .Select(userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = NotificationKind.StageVisitWithdrawn,
                Title = TitleOf(NotificationKind.StageVisitWithdrawn),
                Message = message,
                PathwayId = pathway.Id,
                CreatedAtUtc = nowUtc
            })
            .ToList();
    }

    /// <summary>
    /// After booking or moving an appointment, given where it was before (its status, member of staff and start).
    /// </summary>
    public static IReadOnlyList<Notification> AfterBooking(
        Appointment appointment,
        AppointmentStatus previousStatus,
        Guid? previousStaffUserId,
        DateTime? previousStartUtc,
        PathwayActor actor,
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
                notifications.Add(Create(kind, userId, appointment, message, nowUtc));
        }
    }

    /// <summary>
    /// After recording an appointment that already happened (a walk-in) with someone else: who recorded it, when it was
    /// and its outcome, never the patient.
    /// </summary>
    public static IReadOnlyList<Notification> AfterRecording(Appointment appointment, PathwayActor actor, DateTime nowUtc) =>
        appointment is { StaffUserId: { } staffUserId, StartUtc: { } startUtc }
        && staffUserId != actor.UserId
        && AppointmentWorkflow.IsOutcome(appointment.Status)
            ? [Create(
                NotificationKind.AppointmentRecorded,
                staffUserId,
                appointment,
                $"{actor.Username} recorded an appointment you had on {UkTime.Describe(startUtc)}, as {AppointmentWorkflow.Describe(appointment.Status)}.",
                nowUtc)]
            : [];

    /// <summary>After cancelling an appointment that was booked with someone else.</summary>
    public static IReadOnlyList<Notification> AfterCancelling(
        Appointment appointment,
        AppointmentStatus previousStatus,
        PathwayActor actor,
        DateTime nowUtc) =>
        previousStatus == AppointmentStatus.Booked
        && appointment is { StaffUserId: { } staffUserId, StartUtc: { } startUtc }
        && staffUserId != actor.UserId
            ? [Create(
                NotificationKind.AppointmentCancelled,
                staffUserId,
                appointment,
                $"{actor.Username} cancelled your appointment on {UkTime.Describe(startUtc)}.",
                nowUtc)]
            : [];

    private static Notification Create(
        NotificationKind kind,
        Guid userId,
        Appointment appointment,
        string message,
        DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Kind = kind,
        Title = TitleOf(kind),
        Message = message,
        PathwayId = appointment.PathwayId,
        AppointmentId = appointment.Id,
        CreatedAtUtc = nowUtc
    };
}
