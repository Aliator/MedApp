using FluentValidation;

namespace MedApp.Application.Authentication.Roles.Commands.AssignRole;

public sealed class AssignRoleValidator
    : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role must not be empty.");
    }
}