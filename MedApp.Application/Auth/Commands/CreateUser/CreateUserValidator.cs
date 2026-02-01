using FluentValidation;

namespace MedApp.Application.Auth.Commands.CreateUser;

public sealed class CreateUserValidator
    : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(50);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);
    }
}