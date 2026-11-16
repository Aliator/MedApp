using FluentValidation;
using MedApp.Application.Authentication.TwoFactor.Common;

namespace MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;

public sealed class EnableTwoFactorValidator : AbstractValidator<EnableTwoFactorCommand>
{
    public EnableTwoFactorValidator()
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
