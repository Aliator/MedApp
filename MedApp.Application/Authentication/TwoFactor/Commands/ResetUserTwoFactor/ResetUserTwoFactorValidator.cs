using FluentValidation;

namespace MedApp.Application.Authentication.TwoFactor.Commands.ResetUserTwoFactor;

public sealed class ResetUserTwoFactorValidator : AbstractValidator<ResetUserTwoFactorCommand>
{
    public ResetUserTwoFactorValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");
    }
}
