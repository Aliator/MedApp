using MedApp.Domain.Patients;
using MediatR;
using MedApp.Contracts.Patients.Responses;

namespace MedApp.Application.Patients.Queries.GetPatientById;

/// <summary>
/// A patient's record (patients are centre-wide), with their pathways in teams the viewer can't see into as summary
/// lines only. <paramref name="Scope"/> is the viewer's team walls (from the request; null only outside one, which is
/// unscoped).
/// </summary>
public sealed record GetPatientByIdQuery(Guid Id, MedApp.Application.Common.Teams.TeamScope? Scope = null) : IRequest<PatientResponse?>
{
    /// <summary>The viewer's team walls: the request's, or the system's outside a request.</summary>
    public MedApp.Application.Common.Teams.TeamScope Teams => Scope ?? MedApp.Application.Common.Teams.TeamScope.System;
}