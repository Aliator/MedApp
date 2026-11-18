namespace MedApp.Contracts.Notifications.Responses;

/// <summary>
/// A notification. <c>Kind</c> is its kind's name, e.g. "ProcedureAssigned". It links to the procedure or appointment
/// it's about, if any, or to the Calendar on <c>CalendarDate</c> (a meeting's day). An admin's "Note deletion requested"
/// opens the request (<c>NoteDeletionRequestId</c>) on its procedure's page. A meeting's notification opens
/// the meeting (<c>MeetingId</c>) when it has one.
/// </summary>
public sealed record NotificationResponse(
    Guid Id,
    string Kind,
    string Title,
    string Message,
    Guid? ProcedureId,
    DateTime CreatedAtUtc,
    bool IsRead,
    Guid? AppointmentId = null,
    DateOnly? CalendarDate = null,
    Guid? NoteDeletionRequestId = null,
    Guid? MeetingId = null);
