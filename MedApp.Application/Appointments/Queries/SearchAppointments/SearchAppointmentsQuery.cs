using MedApp.Application.Common.Teams;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Common;
using MediatR;

namespace MedApp.Application.Appointments.Queries.SearchAppointments;

/// <summary>
/// One page of appointments. Every filter is optional; <paramref name="Statuses"/> is any of several, and
/// <paramref name="From"/> and <paramref name="To"/> are UK dates the start falls within (inclusive).
/// <paramref name="View"/> <c>didNotAttend</c> narrows them to the Did not attend list (<c>AppointmentSearch.AwaitingFollowUp</c>),
/// each with how many appointments were missed on its pathway.
/// <para>
/// Only appointments the viewer sees (<paramref name="Scope"/>, the team walls: its own team theirs, or its pathway
/// seen); <paramref name="TeamId"/> (the clinic picker, MC6) narrows them to one team's (its own team). Scope is null
/// only outside a request, which is unscoped.
/// </para>
/// </summary>
public sealed record SearchAppointmentsQuery(
    int Page,
    int PageSize,
    string? Sort,
    bool Ascending,
    Guid? PatientId,
    Guid? PathwayId,
    Guid? StageId,
    Guid? StaffUserId,
    IReadOnlyList<string>? Statuses,
    DateOnly? From,
    DateOnly? To,
    string? Search,
    string? View = null,
    Guid? TeamId = null,
    TeamScope? Scope = null)
    : IRequest<PagedResponse<AppointmentResponse>>
{
    /// <summary>The viewer's team walls: the request's, or the system's outside a request.</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
