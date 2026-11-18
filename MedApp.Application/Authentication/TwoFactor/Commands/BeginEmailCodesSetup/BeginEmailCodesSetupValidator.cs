using FluentValidation;

namespace MedApp.Application.Authentication.TwoFactor.Commands.BeginEmailCodesSetup;

public sealed class BeginEmailCodesSetupValidator : AbstractValidator<BeginEmailCodesSetupCommand>
{
    public BeginEmailCodesSetupValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Enter your password.");
    }
}
