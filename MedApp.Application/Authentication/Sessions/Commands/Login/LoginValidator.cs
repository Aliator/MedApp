using FluentValidation;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Authentication.Sessions.Commands.Login;

public sealed class LoginValidator
    : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.")
            .MaximumLength(UsernameRules.MaxLength)
            .WithMessage($"Username must not exceed {UsernameRules.MaxLength} characters.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password must not be empty.");
    }
}