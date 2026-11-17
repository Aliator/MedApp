using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;

/// <summary>
/// The team view (admins): one UK date of several members of staff's calendars (everyone with a staff role when none
/// are named), side by side, and when they're all free.
/// </summary>
public sealed record GetTeamCalendarQuery(DateOnly Date, IReadOnlyList<Guid> StaffUserIds, CalendarActor Actor)
    : IRequest<TeamCalendarResponse>;
