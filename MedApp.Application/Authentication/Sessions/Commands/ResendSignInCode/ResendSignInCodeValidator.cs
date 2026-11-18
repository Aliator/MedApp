using FluentValidation;
using MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;

namespace MedApp.Application.Authentication.Sessions.Commands.ResendSignInCode;

public sealed class ResendSignInCodeValidator : AbstractValidator<ResendSignInCodeCommand>
{
    public ResendSignInCodeValidator()
    {
        RuleFor(x => x.Challenge)
            .NotEmpty()
            .WithMessage("Sign in again.")
            .MaximumLength(LoginTwoFactorValidator.MaxLength)
            .WithMessage("Sign in again.");
    }
}
