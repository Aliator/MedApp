using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendar;

/// <summary>
/// One person's calendar (the actor's own when <paramref name="StaffUserId"/> is null) from <paramref name="From"/> to
/// <paramref name="To"/>, UK dates, at most 31 days. Any member of staff can see anyone's.
/// </summary>
public sealed record GetCalendarQuery(Guid? StaffUserId, DateOnly From, DateOnly To, CalendarActor Actor)
    : IRequest<CalendarResponse>;
