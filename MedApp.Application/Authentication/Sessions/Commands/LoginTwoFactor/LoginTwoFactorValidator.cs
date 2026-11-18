using FluentValidation;

namespace MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;

public sealed class LoginTwoFactorValidator : AbstractValidator<LoginTwoFactorCommand>
{
    /// <summary>Longer than any challenge token, authenticator code or recovery code, with room for spaces.</summary>
    public const int MaxLength = 64;

    public LoginTwoFactorValidator()
    {
        RuleFor(x => x.Challenge)
            .NotEmpty()
            .WithMessage("Sign in again.")
            .MaximumLength(MaxLength)
            .WithMessage("Sign in again.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .When(x => string.IsNullOrWhiteSpace(x.RecoveryCode))
            .WithMessage("Enter the code from your authenticator app or email.");

        RuleFor(x => x.Code)
            .MaximumLength(MaxLength)
            .WithMessage("That code is too long.");

        RuleFor(x => x.RecoveryCode)
            .MaximumLength(MaxLength)
            .WithMessage("That recovery code is too long.");

        RuleFor(x => x.RecoveryCode)
            .Empty()
            .When(x => !string.IsNullOrWhiteSpace(x.Code))
            .WithMessage("Enter an authenticator code or a recovery code, not both.");
    }
}
