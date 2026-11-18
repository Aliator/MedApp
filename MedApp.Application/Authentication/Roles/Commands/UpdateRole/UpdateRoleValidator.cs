using FluentValidation;
using MedApp.Application.Authentication.Roles.Common;

namespace MedApp.Application.Authentication.Roles.Commands.UpdateRole;

public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("Role id is required.");

        RuleFor(x => x.Name).RoleName();

        RuleFor(x => x.Description).RoleDescription();

        RuleFor(x => x.Permissions)
            .NotNull()
            .WithMessage("Permissions must be given (an empty list leaves only what every role has).");

        RuleForEach(x => x.Permissions).RolePermission();

        RuleFor(x => x.ExpectedVersion)
            .NotEmpty()
            .WithMessage("Expected version is required: send the version you last saw.");
    }
}
