using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetPathwayMdtItems;

public sealed class GetPathwayMdtItemsHandler(
    IMdtItemRepository repository,
    IPatientPathwayRepository pathways,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetPathwayMdtItemsQuery, IReadOnlyList<MdtItemResponse>>
{
    public async Task<IReadOnlyList<MdtItemResponse>> Handle(GetPathwayMdtItemsQuery request, CancellationToken cancellationToken)
    {
        var pathway = await pathways.GetByIdAsync(request.PathwayId, cancellationToken)
                      ?? throw new NotFoundException($"Pathway '{request.PathwayId}' was not found.");
        var items = await repository.GetForPathwayAsync(pathway.Id, cancellationToken);

        await activityLog.RecordViewsAsync(ActivityAction.PathwayViewed, [pathway.PatientId], cancellationToken, pathway.Id);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return items.Select(i => MdtMapper.ToResponse(i, request.Viewer, now)).ToList();
    }
}
