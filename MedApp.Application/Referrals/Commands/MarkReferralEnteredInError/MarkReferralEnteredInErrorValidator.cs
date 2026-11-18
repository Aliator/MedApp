using FluentValidation;
using MedApp.Application.Referrals.Common;

namespace MedApp.Application.Referrals.Commands.MarkReferralEnteredInError;

public sealed class MarkReferralEnteredInErrorValidator : AbstractValidator<MarkReferralEnteredInErrorCommand>
{
    public MarkReferralEnteredInErrorValidator()
    {
        RuleFor(x => x.ReferralId).NotEmpty().WithMessage("Referral id is required.");
        RuleFor(x => x.Detail).Detail();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the referral you changed.");
    }
}
