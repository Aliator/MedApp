using FluentValidation;
using MedApp.Application.Authentication.TwoFactor.Common;

namespace MedApp.Application.Authentication.TwoFactor.Commands.RegenerateRecoveryCodes;

public sealed class RegenerateRecoveryCodesValidator : AbstractValidator<RegenerateRecoveryCodesCommand>
{
    public RegenerateRecoveryCodesValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Enter your password.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Enter the 6-digit code from your authenticator app.")
            .Must(TwoFactorCodes.IsAuthenticatorCode)
            .WithMessage("Enter the 6-digit code from your authenticator app.");
    }
}
