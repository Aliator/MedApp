using FluentValidation;
using MedApp.Application.Authentication.TwoFactor.Common;

namespace MedApp.Application.Authentication.TwoFactor.Commands.EnableEmailCodes;

public sealed class EnableEmailCodesValidator : AbstractValidator<EnableEmailCodesCommand>
{
    public const string EnterCode = "Enter the 6-digit code from the email.";

    public EnableEmailCodesValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage(EnterCode)
            .Must(EmailCodes.IsWellFormed)
            .WithMessage(EnterCode);
    }
}
