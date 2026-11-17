using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Queries.GetMeetingById;

/// <summary>A meeting's details: any member of staff can see them (it's on calendars they can see).</summary>
public sealed record GetMeetingByIdQuery(Guid MeetingId, CalendarActor Actor) : IRequest<MeetingResponse>;
