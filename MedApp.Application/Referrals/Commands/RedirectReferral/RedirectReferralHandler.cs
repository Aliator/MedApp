using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Teams;
using MediatR;

namespace MedApp.Application.Referrals.Commands.RedirectReferral;

/// <summary>
/// Redirects a referral (404 behind the viewer's team wall, recorded; 403 without <c>TriageReferrals</c> or membership of its team; 409 once triaged, on a
/// restricted or anonymised record, or if it changed since the caller's version). To another team here, it stays
/// awaiting triage (a <c>TeamChanged</c> entry; 404 for an unknown team, 409 for an archived one, 400 for
/// its own); elsewhere, it closes (<c>RedirectedElsewhere</c>). Recorded in its history in the same save; whoever
/// registered it is told.
/// </summary>
public sealed class RedirectReferralHandler(
    IReferralRepository repository,
    ITeamRepository teams,
    INotificationSender notificationSender,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<RedirectReferralCommand, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(RedirectReferralCommand request, CancellationToken cancellationToken)
    {
        var referral = await repository.GetForTriageOrThrowAsync(
            request.ReferralId, request.Actor, request.ExpectedVersion, boundary, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ReferralChange(referral, request.Actor, now);
        var detail = request.Detail.Trim();
        var previousTeamName = referral.Team?.Name;
        Team? target = null;

        if (request.TeamId is { } teamId)
        {
            if (teamId == referral.TeamId)
                throw new ValidationException([
                    new ValidationFailure(nameof(RedirectReferralCommand.TeamId), ReferralWorkflow.SameTeamMessage)
                ]);
            target = await teams.GetAsync(teamId, cancellationToken)
                     ?? throw new NotFoundException($"Team '{teamId}' was not found.");
            if (target.IsArchived)
                throw new ConflictException(ReferralWorkflow.ArchivedTeamMessage(target.Name));
            change.RedirectToTeam(target, detail);
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

        // Saved by its ID; shown now with the team read for it.
        if (target is not null)
            referral.Team = target;

        var notification = ReferralNotifications.Triaged(
            referral, request.Actor, now, target is null ? null : previousTeamName);
        if (notification is not null)
            await notificationSender.SendAsync(notification, cancellationToken);

        return await ReferralResponses.ForAsync(referral, repository, request.Actor, timeProvider, cancellationToken);
    }
}
