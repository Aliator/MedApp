using MedApp.Application.Common.Teams;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Queries.GetAppointmentById;

/// <summary>
/// One appointment, if the viewer sees it (<paramref name="Scope"/>, the team walls: its own team theirs, or its pathway
/// seen); otherwise a 404, recorded. Scope is null only outside a request, which is unscoped.
/// </summary>
public sealed record GetAppointmentByIdQuery(Guid AppointmentId, TeamScope? Scope = null) : IRequest<AppointmentResponse>
{
    /// <summary>The viewer's team walls: the request's, or the system's outside a request.</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
