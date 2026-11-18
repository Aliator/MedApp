using MedApp.Application.Activity.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Referrals.Queries.GetReferralById;

/// <summary>
/// One referral. Opening it is recorded against its patient as <see cref="ActivityAction.ReferralViewed"/>, with the
/// referral's ID in the details (once per person and referral every few minutes); the log fails closed.
/// </summary>
public sealed class GetReferralByIdHandler(IReferralRepository repository, IActivityLog activityLog, TimeProvider timeProvider)
    : IRequestHandler<GetReferralByIdQuery, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(GetReferralByIdQuery request, CancellationToken cancellationToken)
    {
        var referral = await repository.GetOrThrowAsync(request.ReferralId, cancellationToken);

        await activityLog.RecordViewAsync(
            ActivityAction.ReferralViewed, referral.PatientId, new { referralId = referral.Id }, cancellationToken);

        return await ReferralResponses.ForAsync(referral, repository, request.Viewer, timeProvider, cancellationToken);
    }
}
