using FluentValidation;
using MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;

namespace MedApp.Application.Authentication.TwoFactor.Commands.DisableTwoFactor;

public sealed class DisableTwoFactorValidator : AbstractValidator<DisableTwoFactorCommand>
{
    public DisableTwoFactorValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Enter your password.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Enter a code from your authenticator app, or a recovery code.")
            .MaximumLength(LoginTwoFactorValidator.MaxLength)
            .WithMessage("That code is too long.");
    }
}
