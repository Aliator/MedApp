using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.EditReferral;

/// <summary>
/// Edits a referral awaiting triage (409 once it's closed, or while its record is restricted or anonymised, or if it
/// changed since the caller's version). A new team must exist and not be archived; the one it has may be. So must
/// a new practice from the directory (none removes the link); an empty typed practice or ODS code takes its. Nothing
/// is saved, or recorded, if nothing changed.
/// </summary>
public sealed class EditReferralHandler(
    IReferralRepository repository,
    ITeamRepository teams,
    IPracticeRepository practices,
    TimeProvider timeProvider)
    : IRequestHandler<EditReferralCommand, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(EditReferralCommand request, CancellationToken cancellationToken)
    {
        var referral = await repository.GetForEditOrThrowAsync(request.ReferralId, cancellationToken);
        ReferralWorkflow.EnsureVersion(referral, request.ExpectedVersion);

        var previous = referral.Team;
        var chosen = await TeamRules.CheckChosenAsync(
            teams, [request.TeamId], new HashSet<Guid> { referral.TeamId }, cancellationToken);
        var team = chosen[request.TeamId];

        var practice = request.PracticeId is { } practiceId
            ? await PracticeRules.CheckChosenAsync(practices, practiceId, referral.PracticeId, cancellationToken)
            : null;

        var details = ReferralDetails.From(
            team.Id,
            request.ReceivedOn,
            ReferralPractices.TypedOrDirectoryName(request.ReferrerPractice, practice),
            request.ReferrerClinician,
            ReferralPractices.TypedOrDirectoryOdsCode(request.ReferrerOdsCode, practice),
            request.RequestedPriority,
            request.Reason,
            practice?.Id);

        var change = new ReferralChange(referral, request.Actor, timeProvider.GetUtcNow().UtcDateTime);
        if (change.EditDetails(details, team, previous))
            await repository.SaveAsync(change.Events, cancellationToken);

        // After saving: the team was read without tracking (see ReferralChange.EditDetails).
        referral.Team = team;
        return await ReferralResponses.ForAsync(referral, repository, request.Actor, timeProvider, cancellationToken);
    }
}
