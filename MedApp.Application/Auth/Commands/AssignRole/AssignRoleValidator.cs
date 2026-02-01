using FluentValidation;

namespace MedApp.Application.Auth.Commands.AssignRole;

public sealed class AssignRoleValidator
    : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty();

        RuleFor(x => x.Role)
            .NotEmpty();
    }
}