using FluentValidation;
using MedApp.Application.Authentication.Passwords.Commands.ResetPassword;

namespace MedApp.Application.Authentication.Emails.Commands.ConfirmEmail;

public sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("This link is incomplete. Open the whole link from the email.");

        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("This link is incomplete. Open the whole link from the email.")
            .MaximumLength(ResetPasswordValidator.MaxTokenLength)
            .WithMessage("This link is incomplete. Open the whole link from the email.");
    }
}
