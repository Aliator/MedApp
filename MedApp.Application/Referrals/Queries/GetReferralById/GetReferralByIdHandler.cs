using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Teams;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Referrals.Queries.GetReferralById;

/// <summary>
/// One referral, seen through the team walls (its current team's, or its pathway's once accepted; admins every
/// team's): behind the viewer's wall it's a 404 with a missing one's words, recorded. Opening it is recorded against
/// its patient as <see cref="ActivityAction.ReferralViewed"/>, with the referral's ID and why the viewer sees it
/// (<c>access</c>, decision 207) in the details (once per person and referral every few minutes); the log fails closed.
/// </summary>
public sealed class GetReferralByIdHandler(
    IReferralRepository repository, ITeamBoundary boundary, IActivityLog activityLog, TimeProvider timeProvider)
    : IRequestHandler<GetReferralByIdQuery, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(GetReferralByIdQuery request, CancellationToken cancellationToken)
    {
        var (referral, access) = await repository.GetVisibleOrThrowAsync(
            request.ReferralId, boundary, request.Viewer.Teams, cancellationToken);

        await activityLog.RecordViewAsync(
            ActivityAction.ReferralViewed,
            referral.PatientId,
            new { referralId = referral.Id, access = access.ToString() },
            cancellationToken);

        return await ReferralResponses.ForAsync(referral, repository, request.Viewer, timeProvider, cancellationToken);
    }
}
