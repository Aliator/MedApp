using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Commands.RejectReferral;

public sealed class RejectReferralValidator : AbstractValidator<RejectReferralCommand>
{
    public static readonly string ReasonNames = string.Join(", ", Enum.GetNames<ReferralRejectionReason>());

    public const string OtherNeedsDetailMessage = "Say why it's rejected: the reason is Other.";

    public RejectReferralValidator()
    {
        RuleFor(x => x.ReferralId).NotEmpty().WithMessage("Referral id is required.");

        RuleFor(x => x.Reason)
            .Must(reason => PathwayEnums.TryParse<ReferralRejectionReason>(reason, out _))
            .WithMessage($"Choose why it's rejected: one of {ReasonNames}.");

        RuleFor(x => x.Detail)
            .Must(detail => detail is null || detail.Trim().Length <= Referral.MaxDetailLength)
            .WithMessage($"The detail must be at most {Referral.MaxDetailLength} characters.");

        RuleFor(x => x.Detail)
            .Must(detail => !string.IsNullOrWhiteSpace(detail))
            .When(x => PathwayEnums.TryParse<ReferralRejectionReason>(x.Reason, out var reason) && reason == ReferralRejectionReason.Other)
            .WithMessage(OtherNeedsDetailMessage);

        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the referral you triaged.");
    }
}
