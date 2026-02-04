using FluentValidation;

namespace MedApp.Application.Auth.Commands.DeleteRole;

public sealed class DeleteRoleValidator
    : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleValidator()
    {
        RuleFor(x => x.RoleName)
            .NotEmpty()
            .WithMessage("Role name must not be empty.")
            .MaximumLength(50)
            .WithMessage("Role name must not exceed 50 characters.");
    }
}