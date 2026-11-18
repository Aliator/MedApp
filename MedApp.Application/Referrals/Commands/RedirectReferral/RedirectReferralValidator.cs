using FluentValidation;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Commands.RedirectReferral;

public sealed class RedirectReferralValidator : AbstractValidator<RedirectReferralCommand>
{
    public const string OneDestinationMessage =
        "Choose where to redirect it: another team here, or elsewhere (another provider), not both.";

    public const string DetailRequiredMessage = "Say why it's redirected.";

    public RedirectReferralValidator()
    {
        RuleFor(x => x.ReferralId).NotEmpty().WithMessage("Referral id is required.");

        RuleFor(x => x)
            .Must(x => (x.TeamId is { } id && id != Guid.Empty) != !string.IsNullOrWhiteSpace(x.Elsewhere))
            .OverridePropertyName(nameof(RedirectReferralCommand.TeamId))
            .WithMessage(OneDestinationMessage);

        RuleFor(x => x.Elsewhere)
            .Must(text => text is null || text.Trim().Length <= Referral.MaxRedirectedToLength)
            .WithMessage($"Where it was sent must be at most {Referral.MaxRedirectedToLength} characters.");

        RuleFor(x => x.Detail)
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage(DetailRequiredMessage)
            .Must(text => text is null || text.Trim().Length <= Referral.MaxDetailLength)
            .WithMessage($"The detail must be at most {Referral.MaxDetailLength} characters.");

        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the referral you triaged.");
    }
}
