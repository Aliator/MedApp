using FluentValidation;

namespace MedApp.Application.Authentication.TwoFactor.Commands.SendTwoFactorEmailCode;

public sealed class SendTwoFactorEmailCodeValidator : AbstractValidator<SendTwoFactorEmailCodeCommand>
{
    public SendTwoFactorEmailCodeValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Enter your password.");
    }
}
