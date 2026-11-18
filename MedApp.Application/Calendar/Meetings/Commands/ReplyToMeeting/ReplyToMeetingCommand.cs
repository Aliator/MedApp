using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.ReplyToMeeting;

/// <summary>
/// The signed-in member of staff accepts or declines a meeting they're going to (<paramref name="Reply"/>: "Accepted"
/// or "Declined"), any time before it ends, changing an earlier reply if they like. Nobody replies for anyone else,
/// admins included. It isn't a change to the meeting, so it carries no <c>expectedLastUpdated</c>; but it's about the
/// time the person saw (<paramref name="ExpectedStartUtc"/> to <paramref name="ExpectedEndUtc"/>, both required), so
/// it's a 409 if the meeting has moved since. A decline tells the organiser and frees the time on the decliner's
/// calendar.
/// </summary>
public sealed record ReplyToMeetingCommand(
    Guid MeetingId,
    string Reply,
    DateTime? ExpectedStartUtc,
    DateTime? ExpectedEndUtc,
    CalendarActor Actor) : IRequest<MeetingResponse>;
