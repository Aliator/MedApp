using FluentValidation;

namespace MedApp.Application.Authentication.Sessions.Commands.SignOutEverywhere;

public sealed class SignOutEverywhereValidator
    : AbstractValidator<SignOutEverywhereCommand>
{
    public SignOutEverywhereValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");
    }
}
