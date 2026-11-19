using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetPathwayMdtItems;

public sealed class GetPathwayMdtItemsHandler(
    IMdtItemRepository repository,
    IPatientPathwayRepository pathways,
    ITeamBoundary boundary,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetPathwayMdtItemsQuery, IReadOnlyList<MdtItemResponse>>
{
    public async Task<IReadOnlyList<MdtItemResponse>> Handle(GetPathwayMdtItemsQuery request, CancellationToken cancellationToken)
    {
        var pathway = await pathways.GetByIdAsync(request.PathwayId, cancellationToken)
                      ?? throw new NotFoundException(PathwayRepositoryExtensions.NotFoundMessage(request.PathwayId));
        // Behind the viewer's team wall: a 404, as for one that isn't there (recorded). Seen, all its items are.
        var access = await boundary.EnsurePathwayVisibleAsync(
            pathway, request.Viewer.Teams, PathwayRepositoryExtensions.NotFoundMessage(request.PathwayId), cancellationToken);
        var items = await repository.GetForPathwayAsync(pathway.Id, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PathwayViewed, [pathway.PatientId], cancellationToken, pathway.Id, new { access = access.ToString() });

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await repository.ToResponsesAsync(items, request.Viewer, now, cancellationToken);
    }
}
