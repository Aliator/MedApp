using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;

/// <summary>
/// An admin arranges a meeting for members of staff: inside one working day, not on a past day, and clash-free on
/// every attendee's calendar. Each attendee but the admin is invited (a notification).
/// </summary>
public sealed record CreateMeetingCommand(
    string Title,
    string? Notes,
    DateTime StartUtc,
    DateTime EndUtc,
    IReadOnlyList<Guid> AttendeeUserIds,
    CalendarActor Actor,
    bool? IsMdt = null,
    Guid? SpecialtyId = null)
    : IRequest<MeetingResponse>, IMeetingCommand;
