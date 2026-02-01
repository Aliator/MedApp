using FluentValidation;

namespace MedApp.Application.Auth.Commands.CreateRole;

public sealed class CreateRoleValidator
    : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(50);
    }
}