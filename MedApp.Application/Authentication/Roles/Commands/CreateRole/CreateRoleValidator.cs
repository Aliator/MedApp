using FluentValidation;

namespace MedApp.Application.Authentication.Roles.Commands.CreateRole;

public sealed class CreateRoleValidator
    : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Role name must not be empty.")
            .MaximumLength(50)
            .WithMessage("Role name must not exceed 50 characters.");
    }
}