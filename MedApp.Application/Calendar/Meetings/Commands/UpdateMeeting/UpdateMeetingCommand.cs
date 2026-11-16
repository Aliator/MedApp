using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;

/// <summary>
/// An admin changes a meeting's title, notes, time and attendees: not on (or to) a past day, inside the working day,
/// clash-free for everyone going. Those taken off are told it's cancelled for them, those added are invited, and the
/// rest are told if it moved.
/// </summary>
public sealed record UpdateMeetingCommand(
    Guid MeetingId,
    string Title,
    string? Notes,
    DateTime StartUtc,
    DateTime EndUtc,
    IReadOnlyList<Guid> AttendeeUserIds,
    CalendarActor Actor)
    : IRequest<MeetingResponse>, IMeetingCommand;
