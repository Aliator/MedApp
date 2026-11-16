using FluentValidation;
using MedApp.Application.Authentication.Emails.Common;
using MedApp.Application.Common.Email;

namespace MedApp.Application.Authentication.Users.Commands.CreateUser;

public sealed class CreateUserValidator
    : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator(EmailAvailability email)
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.")
            .MinimumLength(3)
            .WithMessage("Username must be at least 3 characters long.")
            .MaximumLength(50)
            .WithMessage("Username must not exceed 50 characters.");

        // Optional; one given must be an address, and confirming it needs email to be sent.
        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .ValidEmailAddress();

            RuleFor(x => x.Email)
                .Must(_ => email.CanSendLinks)
                .WithMessage("MedApp can't send email here, so it can't confirm an address. Leave it empty.");
        });
    }
}
