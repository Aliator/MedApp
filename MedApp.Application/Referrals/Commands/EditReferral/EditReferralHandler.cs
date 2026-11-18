using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.EditReferral;

/// <summary>
/// Edits a referral awaiting triage (409 once it's closed, or while its record is restricted or anonymised, or if it
/// changed since the caller's version). A new specialty must exist and not be archived; the one it has may be. Nothing
/// is saved, or recorded, if nothing changed.
/// </summary>
public sealed class EditReferralHandler(
    IReferralRepository repository,
    ISpecialtyRepository specialties,
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

        var details = ReferralDetails.From(
            specialty.Id,
            request.ReceivedOn,
            request.ReferrerPractice,
            request.ReferrerClinician,
            request.ReferrerOdsCode,
            request.RequestedPriority,
            request.Reason);

        var change = new ReferralChange(referral, request.Actor, timeProvider.GetUtcNow().UtcDateTime);
        if (change.EditDetails(details, specialty, previous))
            await repository.SaveAsync(change.Events, cancellationToken);

        // After saving: the specialty was read without tracking (see ReferralChange.EditDetails).
        referral.Specialty = specialty;
        return await ReferralResponses.ForAsync(referral, repository, request.Actor, cancellationToken);
    }
}
