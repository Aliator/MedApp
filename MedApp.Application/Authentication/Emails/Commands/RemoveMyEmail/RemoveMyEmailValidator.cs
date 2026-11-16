using FluentValidation;

namespace MedApp.Application.Authentication.Emails.Commands.RemoveMyEmail;

public sealed class RemoveMyEmailValidator : AbstractValidator<RemoveMyEmailCommand>
{
    public RemoveMyEmailValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Enter your password.");
    }
}
