using FluentValidation;

namespace MedApp.Application.Referrals.Commands.ResumeClock;

public sealed class ResumeClockValidator : AbstractValidator<ResumeClockCommand>
{
    public ResumeClockValidator()
    {
        RuleFor(x => x.ReferralId).NotEmpty().WithMessage("Referral id is required.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the referral you changed.");
    }
}
