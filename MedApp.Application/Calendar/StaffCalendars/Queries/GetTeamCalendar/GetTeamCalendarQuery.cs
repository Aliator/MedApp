using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;

/// <summary>
/// A team's calendar (MC11, anyone): one UK date of its members' calendars side by side, and when they're all free.
/// <paramref name="TeamId"/> is any team; left out, the viewer's first (by name), or for someone in none (an admin) the
/// first team by name. <paramref name="StaffUserIds"/> narrows it to some of the team's members (others are left out);
/// none, everyone in it.
/// </summary>
public sealed record GetTeamCalendarQuery(DateOnly Date, IReadOnlyList<Guid> StaffUserIds, CalendarActor Actor, Guid? TeamId = null)
    : IRequest<TeamCalendarResponse>;
