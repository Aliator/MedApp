using FluentValidation;

namespace MedApp.Application.Auth.Sessions.Commands.Login;

public sealed class LoginValidator
    : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password must not be empty.");
    }
}