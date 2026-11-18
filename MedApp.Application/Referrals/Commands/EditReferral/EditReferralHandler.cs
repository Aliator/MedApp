using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.EditReferral;

/// <summary>
/// Edits a referral awaiting triage (409 once it's closed, or while its record is restricted or anonymised, or if it
/// changed since the caller's version). A new specialty must exist and not be archived; the one it has may be. So must
/// a new practice from the directory (none removes the link); an empty typed practice or ODS code takes its. Nothing
/// is saved, or recorded, if nothing changed.
/// </summary>
public sealed class EditReferralHandler(
    IReferralRepository repository,
    ISpecialtyRepository specialties,
    IPracticeRepository practices,
    TimeProvider timeProvider)
    : IRequestHandler<EditReferralCommand, ReferralResponse>
{
    public async Task<ReferralResponse> Handle(EditReferralCommand request, CancellationToken cancellationToken)
    {
        var referral = await repository.GetForEditOrThrowAsync(request.ReferralId, cancellationToken);
        ReferralWorkflow.EnsureVersion(referral, request.ExpectedVersion);

        var previous = referral.Specialty;
        var chosen = await SpecialtyRules.CheckChosenAsync(
            specialties, [request.SpecialtyId], new HashSet<Guid> { referral.SpecialtyId }, cancellationToken);
        var specialty = chosen[request.SpecialtyId];

        var practice = request.PracticeId is { } practiceId
            ? await PracticeRules.CheckChosenAsync(practices, practiceId, referral.PracticeId, cancellationToken)
            : null;

        var details = ReferralDetails.From(
            specialty.Id,
            request.ReceivedOn,
            ReferralPractices.TypedOrDirectoryName(request.ReferrerPractice, practice),
            request.ReferrerClinician,
            ReferralPractices.TypedOrDirectoryOdsCode(request.ReferrerOdsCode, practice),
            request.RequestedPriority,
            request.Reason,
            practice?.Id);

        var change = new ReferralChange(referral, request.Actor, timeProvider.GetUtcNow().UtcDateTime);
        if (change.EditDetails(details, specialty, previous))
            await repository.SaveAsync(change.Events, cancellationToken);

        // After saving: the specialty was read without tracking (see ReferralChange.EditDetails).
        referral.Specialty = specialty;
        return await ReferralResponses.ForAsync(referral, repository, request.Actor, cancellationToken);
    }
}
