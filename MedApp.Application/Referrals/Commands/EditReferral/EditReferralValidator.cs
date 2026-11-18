using FluentValidation;
using MedApp.Application.Referrals.Common;

namespace MedApp.Application.Referrals.Commands.EditReferral;

public sealed class EditReferralValidator : AbstractValidator<EditReferralCommand>
{
    public EditReferralValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.ReferralId).NotEmpty().WithMessage("Referral id is required.");
        RuleFor(x => x.SpecialtyId).SpecialtyId();
        RuleFor(x => x.ReceivedOn).ReceivedOn(timeProvider);
        // With a practice from the directory, an empty practice takes its name.
        When(x => x.PracticeId is null || !string.IsNullOrWhiteSpace(x.ReferrerPractice),
            () => RuleFor(x => x.ReferrerPractice).ReferrerPractice());
        RuleFor(x => x.PracticeId)
            .Must(id => id != Guid.Empty)
            .WithMessage("Practice id must not be empty: leave it out for none.");
        RuleFor(x => x.ReferrerClinician).ReferrerClinician();
        RuleFor(x => x.ReferrerOdsCode).ReferrerOdsCode();
        RuleFor(x => x.RequestedPriority).RequestedPriority();
        RuleFor(x => x.Reason).Reason();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the referral you changed.");
    }
}
