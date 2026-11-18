namespace MedApp.Domain.Notifications;

/// <summary>
/// Something that happened that a user should know about, e.g. being assigned to a procedure. Kept, so it's there
/// after signing in again, and marked read once they've seen it. Like the audit log, it has no foreign keys, so it
/// outlives the procedure it's about.
/// </summary>
public sealed class Notification
{
    public Guid Id { get; init; }

    /// <summary>Who it's for.</summary>
    public Guid UserId { get; init; }

    public NotificationKind Kind { get; init; }

    /// <summary>One line: the procedure's title, or a fixed phrase for the kind (<see cref="HasFixedTitle"/>).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>What happened, e.g. "admin assigned you to it."</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The procedure it's about, if any, for a link to it.</summary>
    public Guid? ProcedureId { get; init; }

    /// <summary>The appointment it's about, if any, for a link to it.</summary>
    public Guid? AppointmentId { get; init; }

    /// <summary>The UK date on the calendar it's about, if any (a meeting's day), for a link to the Calendar that day.</summary>
    public DateOnly? CalendarDate { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>When the user marked it read; null while unread.</summary>
    public DateTime? ReadAtUtc { get; set; }

    /// <summary>
    /// Whether it's a monitoring notification: an admin told about what a member of staff did. It's about the staff,
    /// not the patient, so a patient's export leaves it out.
    /// </summary>
    public static bool IsMonitoring(NotificationKind kind) =>
        kind is >= NotificationKind.MonitoredProcedureCreated and <= NotificationKind.MonitoredDueDateChanged;

    /// <summary>
    /// Whether notifications of this kind have a fixed title (the calendar kinds, 1 to 9: the appointment kinds'
    /// "Appointment booked", the meeting kinds' "Meeting invitation"; and the monitoring kinds' "Stage finished"),
    /// rather than the procedure's title, so anonymising a patient leaves it as it is.
    /// </summary>
    public static bool HasFixedTitle(NotificationKind kind) =>
        kind is >= NotificationKind.AppointmentBooked and <= NotificationKind.AppointmentRecorded || IsMonitoring(kind);
}
