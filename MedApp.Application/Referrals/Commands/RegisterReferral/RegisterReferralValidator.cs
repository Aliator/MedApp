using FluentValidation;
using MedApp.Application.Referrals.Common;

namespace MedApp.Application.Referrals.Commands.RegisterReferral;

public sealed class RegisterReferralValidator : AbstractValidator<RegisterReferralCommand>
{
    public RegisterReferralValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Choose the patient.");
        RuleFor(x => x.SpecialtyId).SpecialtyId();
        RuleFor(x => x.ReceivedOn).ReceivedOn(timeProvider);
        RuleFor(x => x.ReferrerPractice).ReferrerPractice();
        RuleFor(x => x.ReferrerClinician).ReferrerClinician();
        RuleFor(x => x.ReferrerOdsCode).ReferrerOdsCode();
        RuleFor(x => x.RequestedPriority).RequestedPriority();
        RuleFor(x => x.Reason).Reason();
    }
}
