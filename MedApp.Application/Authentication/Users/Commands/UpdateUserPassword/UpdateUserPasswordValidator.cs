using FluentValidation;

namespace MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;

public sealed class UpdateUserValidator
    : AbstractValidator<UpdateUserPasswordCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.")
            .MinimumLength(3)
            .WithMessage("Username must be at least 3 characters long.")
            .MaximumLength(50)
            .WithMessage("Username must not exceed 50 characters.");

        RuleFor(x => x.OldPassword)
            .NotEmpty()
            .WithMessage("Password must not be empty.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("Password must not be empty.")
            // Exactly the same, case and all (passwords are case-sensitive). Otherwise a generated password could be
            // "changed" to itself, which would clear the requirement to choose one's own.
            .NotEqual(x => x.OldPassword, StringComparer.Ordinal)
            .WithMessage("The new password must be different from the current one.");
    }
}