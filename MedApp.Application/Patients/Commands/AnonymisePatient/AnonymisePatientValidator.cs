using FluentValidation;

namespace MedApp.Application.Patients.Commands.AnonymisePatient;

public sealed class AnonymisePatientValidator : AbstractValidator<AnonymisePatientCommand>
{
    public const int MaxReasonLength = 500;

    public AnonymisePatientValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Give a reason.")
            .MaximumLength(MaxReasonLength)
            .WithMessage($"The reason must not exceed {MaxReasonLength} characters.");

        RuleFor(x => x.Confirmation)
            .NotEmpty()
            .WithMessage("Type the patient's full name to confirm.");
    }
}
