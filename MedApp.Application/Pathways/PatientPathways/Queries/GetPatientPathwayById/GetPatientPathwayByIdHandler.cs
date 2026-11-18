using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;

/// <summary>A pathway in full, with the referral it was accepted from, if any. Opening it is recorded in the activity log, against its patient (once per person every few minutes).</summary>
public sealed class GetPatientPathwayByIdHandler(
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    IReferralRepository referralRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetPatientPathwayByIdQuery, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(GetPatientPathwayByIdQuery request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetByIdAsync(request.Id, cancellationToken)
                        ?? throw new NotFoundException($"Pathway '{request.Id}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.PathwayViewed, [pathway.PatientId], cancellationToken, pathway.Id);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var response = await PathwayMapper.ToResponseAsync(
            pathway, identityReadService, now, cancellationToken, viewerId: request.ViewerId, viewerPermissions: request.ViewerPermissions);

        // The referral it was accepted from, if any, for a link back to it and its waiting-time clock.
        if (await referralRepository.GetForPathwayAsync(pathway.Id, cancellationToken) is { } referral)
            response.Referral = ReferralMapper.ToPathwayReferral(
                referral, new PathwayActor(request.ViewerId ?? Guid.Empty, string.Empty, request.ViewerPermissions), DueDates.ToUkDate(now));
        return response;
    }
}
