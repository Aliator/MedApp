using FluentValidation;

namespace MedApp.Application.Patients.Commands.SetPatientFlag;

public sealed class SetPatientFlagValidator : AbstractValidator<SetPatientFlagCommand>
{
    public const int MaxReasonLength = 500;

    public SetPatientFlagValidator()
    {
        RuleFor(x => x.Flag)
            .IsInEnum()
            .WithMessage("Flag must be Restricted or Disputed.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => x.Set)
            .WithMessage("Give a reason.");

        RuleFor(x => x.Reason)
            .MaximumLength(MaxReasonLength)
            .WithMessage($"The reason must not exceed {MaxReasonLength} characters.");
    }
}
