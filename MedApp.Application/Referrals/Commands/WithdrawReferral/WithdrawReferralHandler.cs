using MedApp.Application.Common.Teams;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.WithdrawReferral;

/// <summary>
/// Closes a referral as withdrawn (by its own team: 404 behind the viewer's team wall, 409 for another team's; 409 unless it awaits triage, or if its record is anonymised, or if it changed since the
/// caller's version), recorded in its history in the same save.
/// </summary>
public sealed class WithdrawReferralHandler(
    IReferralRepository repository, ITeamBoundary boundary, TimeProvider timeProvider)
    : IRequestHandler<WithdrawReferralCommand, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(WithdrawReferralCommand request, CancellationToken cancellationToken)
    {
        var referral = await repository.GetForCloseOrThrowAsync(
            request.ReferralId, boundary, request.Actor.Teams, cancellationToken);
        ReferralWorkflow.EnsureVersion(referral, request.ExpectedVersion);

        var change = new ReferralChange(referral, request.Actor, timeProvider.GetUtcNow().UtcDateTime);
        change.Withdraw(request.Detail.Trim());
        await repository.SaveAsync(change.Events, cancellationToken);

        return await ReferralResponses.ForAsync(referral, repository, request.Actor, timeProvider, cancellationToken);
    }
}
