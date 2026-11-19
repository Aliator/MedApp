using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consults.Responses;
using MediatR;

namespace MedApp.Application.Consults.Queries.GetPathwayConsults;

/// <summary>
/// A pathway's consults, newest sent first, and whether the viewer may send another (<c>canRequest</c>). Behind the
/// viewer's wall it's a 404 (recorded); reading them records <c>PathwayViewed</c> with the viewer's <c>access</c>.
/// </summary>
public sealed record GetPathwayConsultsQuery(Guid PathwayId, PathwayActor Viewer) : IRequest<PathwayConsultsResponse>;
