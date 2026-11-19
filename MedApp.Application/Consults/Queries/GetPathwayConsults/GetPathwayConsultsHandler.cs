using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consults.Common;
using MedApp.Application.Consults.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consults.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Consults.Queries.GetPathwayConsults;

public sealed class GetPathwayConsultsHandler(
    IConsultRepository repository,
    ITeamBoundary boundary,
    IActivityLog activityLog)
    : IRequestHandler<GetPathwayConsultsQuery, PathwayConsultsResponse>
{
    public async Task<PathwayConsultsResponse> Handle(GetPathwayConsultsQuery request, CancellationToken cancellationToken)
    {
        var notFound = PathwayRepositoryExtensions.NotFoundMessage(request.PathwayId);
        var pathway = await repository.GetPathwayAsync(request.PathwayId, cancellationToken)
                      ?? throw new NotFoundException(notFound);
        var access = await boundary.EnsurePathwayVisibleAsync(pathway, request.Viewer.Teams, notFound, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PathwayViewed, [pathway.PatientId], cancellationToken, pathway.Id, new { access = access.ToString() });

        var consults = await repository.GetForPathwayAsync(pathway.Id, cancellationToken);
        return new PathwayConsultsResponse
        {
            Consults = consults.Select(c => ConsultMapper.ToResponse(c, request.Viewer)).ToList(),
            CanRequest = ConsultMapper.CanRequest(pathway, request.Viewer),
        };
    }
}
