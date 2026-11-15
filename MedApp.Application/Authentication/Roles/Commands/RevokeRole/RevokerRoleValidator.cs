using FluentValidation;

namespace MedApp.Application.Authentication.Roles.Commands.RevokeRole;

public sealed class RevokeRoleValidator
    : AbstractValidator<RevokeRoleCommand>
{
    public RevokeRoleValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role must not be empty.");
    }
}