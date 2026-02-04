using FluentValidation;

namespace MedApp.Application.Auth.Commands.UpdateUserPassword;

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
            .WithMessage("Password must not be empty.");
    }
}