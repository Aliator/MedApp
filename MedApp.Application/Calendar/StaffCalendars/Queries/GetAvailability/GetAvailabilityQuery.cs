using MedApp.Application.Common.Teams;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetAvailability;

/// <summary>
/// When members of staff are all free from one UK date to another (at most 31 days), for arranging a meeting of
/// <paramref name="DurationMinutes"/>: the free time each working day, suggested times, and each person's busy times.
/// A meeting the viewer can't see (<paramref name="Scope"/>, the team walls: <see cref="Common.CalendarWalls"/>) is
/// busy as "a meeting", without its title. Scope is null only outside a request, which is unscoped.
/// </summary>
public sealed record GetAvailabilityQuery(
    IReadOnlyList<Guid> StaffUserIds,
    DateOnly From,
    DateOnly To,
    int DurationMinutes,
    Guid? ExceptMeetingId,
    TeamScope? Scope = null)
    : IRequest<AvailabilityResponse>
{
    /// <summary>The viewer's team walls: the request's, or the system's outside a request.</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
