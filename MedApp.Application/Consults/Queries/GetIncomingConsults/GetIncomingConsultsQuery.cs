using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consults.Responses;
using MediatR;

namespace MedApp.Application.Consults.Queries.GetIncomingConsults;

/// <summary>
/// The open consults (open or replied) asked of the viewer's teams (an admin's: every team's), in one team (one of
/// their own, or any for an admin: else a 400 on <c>TeamId</c>) or all, oldest sent first. Records <c>PatientListed</c>
/// for each patient shown.
/// </summary>
public sealed record GetIncomingConsultsQuery(Guid? TeamId, PathwayActor Viewer) : IRequest<IReadOnlyList<ConsultResponse>>;
