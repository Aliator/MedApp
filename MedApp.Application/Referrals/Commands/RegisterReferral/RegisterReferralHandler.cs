using MedApp.Application.Common.Identity;
using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Referrals;
using MediatR;

namespace MedApp.Application.Referrals.Commands.RegisterReferral;

/// <summary>
/// Registers a referral. In order: the patient exists (404) and their record can change (409, saying to give the letter
/// to an admin: hazard H-40); the team exists (404) and isn't archived (409); unless confirmed, the patient has no
/// open referral to it (a 400 saying when those were received: hazard H-44). A practice from the directory must exist
/// (404) and not be archived (409); an empty typed practice or ODS code takes the directory's. The referral and its Registered entry are
/// saved together; the change is recorded in its history, not the activity log, so it isn't written twice.
/// </summary>
public sealed class RegisterReferralHandler(
    IReferralRepository repository,
    IPatientRepository patients,
    ITeamRepository teams,
    IPracticeRepository practices,
    TimeProvider timeProvider)
    : IRequestHandler<RegisterReferralCommand, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(RegisterReferralCommand request, CancellationToken cancellationToken)
    {
        var patient = await patients.GetByIdAsync(request.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        if (PatientRules.IsAnonymised(patient))
            throw new ConflictException(ReferralWorkflow.AnonymisedRegisterMessage);
        if (PatientRules.IsRestricted(patient))
            throw new ConflictException(ReferralWorkflow.RestrictedRegisterMessage);

        var chosen = await TeamRules.CheckChosenAsync(
            teams, [request.TeamId], new HashSet<Guid>(), cancellationToken);
        var team = chosen[request.TeamId];

        if (!request.ConfirmPossibleDuplicate)
        {
            var open = await repository.GetOpenForPatientAsync(patient.Id, team.Id, cancellationToken);
            if (open.Count > 0)
                throw new ValidationException([
                    new ValidationFailure(
                        nameof(request.ConfirmPossibleDuplicate),
                        ReferralWorkflow.PossibleDuplicateMessage(team.Name, open))
                ]);
        }

        var practice = request.PracticeId is { } practiceId
            ? await PracticeRules.CheckChosenAsync(practices, practiceId, null, cancellationToken)
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
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var referral = new Referral
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            TeamId = team.Id,
            ReceivedOn = details.ReceivedOn,
            ReferrerPractice = details.ReferrerPractice,
            ReferrerClinician = details.ReferrerClinician,
            ReferrerOdsCode = details.ReferrerOdsCode,
            PracticeId = details.PracticeId,
            RequestedPriority = details.RequestedPriority,
            Reason = details.Reason,
            Status = ReferralStatus.AwaitingTriage,
            RegisteredAtUtc = now,
            RegisteredByUserId = request.Actor.UserId,
            RegisteredByUsername = request.Actor.Username,
            LastUpdated = now
        };

        var change = new ReferralChange(referral, request.Actor, now);
        change.Register(team);
        await repository.AddAsync(referral, change.Events, cancellationToken);

        // Only now: the patient and team were read without tracking, so saving the referral with them attached
        // would try to add them again.
        referral.Patient = patient;
        referral.Team = team;

        // The registrant may triage it too, if they're a clinician of its team.
        var isMember = request.Actor.Has(Permission.TriageReferrals)
                       && await repository.IsTeamMemberAsync(request.Actor.UserId, referral.TeamId, cancellationToken);
        return ReferralMapper.ToResponse(
            referral, change.Events, letterCount: 0, request.Actor, isMember, DueDates.ToUkDate(now));
    }
}
