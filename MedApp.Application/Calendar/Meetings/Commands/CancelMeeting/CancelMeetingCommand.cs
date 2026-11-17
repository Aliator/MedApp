using MedApp.Application.Calendar.Common;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.CancelMeeting;

/// <summary>An admin cancels a meeting, which deletes it, not on a past day. Each attendee but the admin is told.</summary>
public sealed record CancelMeetingCommand(Guid MeetingId, CalendarActor Actor) : IRequest;
