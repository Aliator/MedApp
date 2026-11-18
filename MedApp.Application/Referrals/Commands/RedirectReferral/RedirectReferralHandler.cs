using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Specialties;
using MediatR;

namespace MedApp.Application.Referrals.Commands.RedirectReferral;

/// <summary>
/// Redirects a referral (403 without <c>TriageReferrals</c> or membership of its specialty; 409 once triaged, on a
/// restricted or anonymised record, or if it changed since the caller's version). To another specialty here, it stays
/// awaiting triage (a <c>SpecialtyChanged</c> entry; 404 for an unknown specialty, 409 for an archived one, 400 for
/// its own); elsewhere, it closes (<c>RedirectedElsewhere</c>). Recorded in its history in the same save; whoever
/// registered it is told.
/// </summary>
public sealed class RedirectReferralHandler(
    IReferralRepository repository,
    ISpecialtyRepository specialties,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<RedirectReferralCommand, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(RedirectReferralCommand request, CancellationToken cancellationToken)
    {
        var referral = await repository.GetForTriageOrThrowAsync(
            request.ReferralId, request.Actor, request.ExpectedVersion, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ReferralChange(referral, request.Actor, now);
        var detail = request.Detail.Trim();
        var previousSpecialtyName = referral.Specialty?.Name;
        Specialty? target = null;

        if (request.SpecialtyId is { } specialtyId)
        {
            if (specialtyId == referral.SpecialtyId)
                throw new ValidationException([
                    new ValidationFailure(nameof(RedirectReferralCommand.SpecialtyId), ReferralWorkflow.SameSpecialtyMessage)
                ]);
            target = await specialties.GetAsync(specialtyId, cancellationToken)
                     ?? throw new NotFoundException($"Specialty '{specialtyId}' was not found.");
            if (target.IsArchived)
                throw new ConflictException(ReferralWorkflow.ArchivedSpecialtyMessage(target.Name));
            change.RedirectToSpecialty(target, detail);
        }
        else
            change.RedirectElsewhere(request.Elsewhere!.Trim(), detail);

        try
        {
            await repository.SaveAsync(change.Events, cancellationToken);
        }
        catch (ConflictException)
        {
            // Another triager decided it, or someone changed it, since it was read.
            throw await repository.TriageConflictAsync(referral.Id, cancellationToken);
        }

        // Saved by its ID; shown now with the specialty read for it.
        if (target is not null)
            referral.Specialty = target;

        var notification = ReferralNotifications.Triaged(
            referral, request.Actor, now, target is null ? null : previousSpecialtyName);
        if (notification is not null)
            await notificationSender.SendAsync(notification, cancellationToken);

        return await ReferralResponses.ForAsync(referral, repository, request.Actor, cancellationToken);
    }
}
