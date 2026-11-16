using FluentValidation;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Authentication.Passwords.Commands.ForgotPassword;

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty()
            .WithMessage("Enter your username or email address.")
            .MaximumLength(UsernameRules.MaxLength)
            .WithMessage($"That is longer than any username or email address ({UsernameRules.MaxLength} characters).");
    }
}
