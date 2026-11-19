using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Queries.GetMeetingById;

/// <summary>
/// A meeting's details, for one the viewer sees (MC11): they're in it, it's their team's MDT, or anyone in it shares a team
/// with them (admins: every one). Any other is a 404, recorded.
/// </summary>
public sealed record GetMeetingByIdQuery(Guid MeetingId, CalendarActor Actor) : IRequest<MeetingResponse>;
