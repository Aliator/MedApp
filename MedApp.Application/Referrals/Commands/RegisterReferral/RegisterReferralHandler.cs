using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Referrals;
using MediatR;

namespace MedApp.Application.Referrals.Commands.RegisterReferral;

/// <summary>
/// Registers a referral. In order: the patient exists (404) and their record can change (409, saying to give the letter
/// to an admin: hazard H-40); the specialty exists (404) and isn't archived (409); unless confirmed, the patient has no
/// open referral to it (a 400 saying when those were received: hazard H-44). The referral and its Registered entry are
/// saved together; the change is recorded in its history, not the activity log, so it isn't written twice.
/// </summary>
public sealed class RegisterReferralHandler(
    IReferralRepository repository,
    IPatientRepository patients,
    ISpecialtyRepository specialties,
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

        var chosen = await SpecialtyRules.CheckChosenAsync(
            specialties, [request.SpecialtyId], new HashSet<Guid>(), cancellationToken);
        var specialty = chosen[request.SpecialtyId];

        if (!request.ConfirmPossibleDuplicate)
        {
            var open = await repository.GetOpenForPatientAsync(patient.Id, specialty.Id, cancellationToken);
            if (open.Count > 0)
                throw new ValidationException([
                    new ValidationFailure(
                        nameof(request.ConfirmPossibleDuplicate),
                        ReferralWorkflow.PossibleDuplicateMessage(specialty.Name, open))
                ]);
        }

        var details = ReferralDetails.From(
            specialty.Id,
            request.ReceivedOn,
            request.ReferrerPractice,
            request.ReferrerClinician,
            request.ReferrerOdsCode,
            request.RequestedPriority,
            request.Reason);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var referral = new Referral
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            SpecialtyId = specialty.Id,
            ReceivedOn = details.ReceivedOn,
            ReferrerPractice = details.ReferrerPractice,
            ReferrerClinician = details.ReferrerClinician,
            ReferrerOdsCode = details.ReferrerOdsCode,
            RequestedPriority = details.RequestedPriority,
            Reason = details.Reason,
            Status = ReferralStatus.AwaitingTriage,
            RegisteredAtUtc = now,
            RegisteredByUserId = request.Actor.UserId,
            RegisteredByUsername = request.Actor.Username,
            LastUpdated = now
        };

        var change = new ReferralChange(referral, request.Actor, now);
        change.Register(specialty);
        await repository.AddAsync(referral, change.Events, cancellationToken);

        // Only now: the patient and specialty were read without tracking, so saving the referral with them attached
        // would try to add them again.
        referral.Patient = patient;
        referral.Specialty = specialty;

        return ReferralMapper.ToResponse(referral, change.Events, letterCount: 0, request.Actor);
    }
}
