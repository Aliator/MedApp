using MedApp.Application.Common.Exceptions;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Referrals;
using MediatR;

namespace MedApp.Application.Referrals.Commands.RejectReferral;

/// <summary>
/// Rejects a referral (403 without <c>TriageReferrals</c> or membership of its specialty; 409 once triaged, on a
/// restricted or anonymised record, or if it changed since the caller's version), recorded in its history in the same
/// save. Whoever registered it is told.
/// </summary>
public sealed class RejectReferralHandler(
    IReferralRepository repository,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<RejectReferralCommand, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(RejectReferralCommand request, CancellationToken cancellationToken)
    {
        var referral = await repository.GetForTriageOrThrowAsync(
            request.ReferralId, request.Actor, request.ExpectedVersion, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ReferralChange(referral, request.Actor, now);
        var detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim();
        change.Reject(PathwayEnums.Parse<ReferralRejectionReason>(request.Reason), detail);

        try
        {
            await repository.SaveAsync(change.Events, cancellationToken);
        }
        catch (ConflictException)
        {
            // Another triager decided it, or someone changed it, since it was read.
            throw await repository.TriageConflictAsync(referral.Id, cancellationToken);
        }

        if (ReferralNotifications.Triaged(referral, request.Actor, now) is { } notification)
            await notificationSender.SendAsync(notification, cancellationToken);

        return await ReferralResponses.ForAsync(referral, repository, request.Actor, timeProvider, cancellationToken);
    }
}
