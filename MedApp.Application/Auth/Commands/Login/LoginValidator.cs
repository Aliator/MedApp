using FluentValidation;

namespace MedApp.Application.Auth.Commands.Login;

public sealed class LoginValidator
    : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}