using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;

/// <summary>
/// An admin changes a meeting's title, notes, time and attendees: not on (or to) a past day, inside the working day,
/// clash-free for everyone going. Those taken off are told it's cancelled for them, those added are invited, and the
/// rest are told if it moved. <paramref name="ExpectedLastUpdated"/> is the meeting's <c>LastUpdated</c> as the caller
/// last read it: if anyone has changed it since, the change is refused (a 409), so a stale screen can't undo theirs.
/// </summary>
public sealed record UpdateMeetingCommand(
    Guid MeetingId,
    string Title,
    string? Notes,
    DateTime StartUtc,
    DateTime EndUtc,
    IReadOnlyList<Guid> AttendeeUserIds,
    DateTime? ExpectedLastUpdated,
    CalendarActor Actor,
    bool? IsMdt = null,
    Guid? SpecialtyId = null)
    : IRequest<MeetingResponse>, IMeetingCommand;
