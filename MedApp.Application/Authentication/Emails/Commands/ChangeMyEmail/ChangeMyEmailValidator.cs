using FluentValidation;
using MedApp.Application.Authentication.Emails.Common;

namespace MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail;

public sealed class ChangeMyEmailValidator : AbstractValidator<ChangeMyEmailCommand>
{
    public ChangeMyEmailValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Enter an email address.")
            .ValidEmailAddress();
    }
}
