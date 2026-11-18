using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;

/// <summary>A pathway in full. Opening it is recorded in the activity log, against its patient (once per person every few minutes).</summary>
public sealed class GetPatientPathwayByIdHandler(
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetPatientPathwayByIdQuery, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(GetPatientPathwayByIdQuery request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetByIdAsync(request.Id, cancellationToken)
                        ?? throw new NotFoundException($"Pathway '{request.Id}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.PathwayViewed, [pathway.PatientId], cancellationToken, pathway.Id);

        return await PathwayMapper.ToResponseAsync(
            pathway, identityReadService, timeProvider.GetUtcNow().UtcDateTime, cancellationToken, viewerId: request.ViewerId, viewerIsAdmin: request.ViewerIsAdmin);
    }
}
