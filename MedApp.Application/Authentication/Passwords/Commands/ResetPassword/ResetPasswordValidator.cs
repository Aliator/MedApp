using FluentValidation;

namespace MedApp.Application.Authentication.Passwords.Commands.ResetPassword;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    /// <summary>Far longer than any reset token Identity makes, so an absurd one is refused before it's read.</summary>
    public const int MaxTokenLength = 2048;

    public ResetPasswordValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("This link is incomplete. Open the whole link from the email.");

        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("This link is incomplete. Open the whole link from the email.")
            .MaximumLength(MaxTokenLength)
            .WithMessage("This link is incomplete. Open the whole link from the email.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("Password must not be empty.");
    }
}
