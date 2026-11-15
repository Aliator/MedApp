using FluentValidation;

namespace MedApp.Application.Authentication.Roles.Commands.SetUserRoles;

public sealed class SetUserRolesValidator
    : AbstractValidator<SetUserRolesCommand>
{
    public SetUserRolesValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");

        RuleFor(x => x.Roles)
            .NotNull()
            .WithMessage("Roles must be given (an empty list removes every role).");

        RuleForEach(x => x.Roles)
            .NotEmpty()
            .WithMessage("Role must not be empty.");
    }
}
