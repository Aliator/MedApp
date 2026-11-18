using FluentValidation;
using MedApp.Application.Authentication.Roles.Common;

namespace MedApp.Application.Authentication.Roles.Commands.CreateRole;

public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name).RoleName();

        RuleFor(x => x.Description).RoleDescription();

        RuleFor(x => x.Permissions)
            .NotNull()
            .WithMessage("Permissions must be given (an empty list leaves only what every role has).");

        RuleForEach(x => x.Permissions).RolePermission();
    }
}
