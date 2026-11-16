using FluentValidation;

namespace MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail;

public sealed class RemoveUserEmailValidator : AbstractValidator<RemoveUserEmailCommand>
{
    public RemoveUserEmailValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");
    }
}
