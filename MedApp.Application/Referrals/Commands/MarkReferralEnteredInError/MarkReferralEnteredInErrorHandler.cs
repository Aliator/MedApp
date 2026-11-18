using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.MarkReferralEnteredInError;

/// <summary>
/// Closes a referral as entered in error (409 unless it awaits triage, or if its record is anonymised, or if it changed since the
/// caller's version), recorded in its history in the same save.
/// </summary>
public sealed class MarkReferralEnteredInErrorHandler(IReferralRepository repository, TimeProvider timeProvider)
    : IRequestHandler<MarkReferralEnteredInErrorCommand, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(MarkReferralEnteredInErrorCommand request, CancellationToken cancellationToken)
    {
        var referral = await repository.GetForCloseOrThrowAsync(request.ReferralId, cancellationToken);
        ReferralWorkflow.EnsureVersion(referral, request.ExpectedVersion);

        var change = new ReferralChange(referral, request.Actor, timeProvider.GetUtcNow().UtcDateTime);
        change.MarkEnteredInError(request.Detail.Trim());
        await repository.SaveAsync(change.Events, cancellationToken);

        return await ReferralResponses.ForAsync(referral, repository, request.Actor, timeProvider, cancellationToken);
    }
}
