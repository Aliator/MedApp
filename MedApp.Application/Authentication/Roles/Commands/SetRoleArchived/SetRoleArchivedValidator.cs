using FluentValidation;

namespace MedApp.Application.Authentication.Roles.Commands.SetRoleArchived;

public sealed class SetRoleArchivedValidator : AbstractValidator<SetRoleArchivedCommand>
{
    public SetRoleArchivedValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("Role id is required.");

        RuleFor(x => x.ExpectedVersion)
            .NotEmpty()
            .WithMessage("Expected version is required: send the version you last saw.");
    }
}
