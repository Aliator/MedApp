using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;

/// <summary>
/// A pathway in full, with the referral it was accepted from, if any. Behind the viewer's team wall it's a 404, as if it
/// weren't there, recorded (<see cref="ITeamBoundary"/>). Opening it is recorded in the activity log, against its patient
/// (once per person every few minutes), with why they could (<c>access</c>: owner, stage owner, consult, MDT or admin).
/// </summary>
public sealed class GetPatientPathwayByIdHandler(
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    IReferralRepository referralRepository,
    ITeamBoundary boundary,
    MedApp.Application.Teams.Repositories.ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<GetPatientPathwayByIdQuery, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(GetPatientPathwayByIdQuery request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetByIdAsync(request.Id, cancellationToken)
                        ?? throw new NotFoundException(PathwayRepositoryExtensions.NotFoundMessage(request.Id));
        var access = await boundary.EnsurePathwayVisibleAsync(
            pathway, request.Teams, PathwayRepositoryExtensions.NotFoundMessage(request.Id), cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PathwayViewed, [pathway.PatientId], cancellationToken, pathway.Id, new { access = access.ToString() });

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var teamName = TeamAccess.CanChange(pathway, request.Teams)
            ? null
            : (await teams.GetAsync(pathway.TeamId, cancellationToken))?.Name;
        var response = await PathwayMapper.ToResponseAsync(
            pathway, identityReadService, now, cancellationToken, viewerId: request.ViewerId,
            viewerPermissions: request.ViewerPermissions, viewerScope: request.Teams, teamName: teamName, viewerAccess: access);

        // The referral it was accepted from, if any, for a link back to it and its waiting-time clock.
        if (await referralRepository.GetForPathwayAsync(pathway.Id, cancellationToken) is { } referral)
            response.Referral = ReferralMapper.ToPathwayReferral(
                referral, new PathwayActor(request.ViewerId ?? Guid.Empty, string.Empty, request.ViewerPermissions, request.Scope), DueDates.ToUkDate(now));
        return response;
    }
}
