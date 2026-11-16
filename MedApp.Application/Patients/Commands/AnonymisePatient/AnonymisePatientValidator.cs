using FluentValidation;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Patients.Commands.AnonymisePatient;

public sealed class AnonymisePatientValidator : AbstractValidator<AnonymisePatientCommand>
{
    public const int MaxNoteLength = 500;

    public static string ReasonNames => string.Join(", ", Enum.GetNames<AnonymisationReason>());

    public AnonymisePatientValidator()
    {
        RuleFor(x => x.Reason)
            .Must(reason => ProcedureEnums.TryParse<AnonymisationReason>(reason, out _))
            .WithMessage($"Choose a reason: one of {ReasonNames}.");

        RuleFor(x => x.Note)
            .MaximumLength(MaxNoteLength)
            .WithMessage($"The note must not exceed {MaxNoteLength} characters.");

        RuleFor(x => x.Confirmation)
            .NotEmpty()
            .WithMessage("Type the patient's full name to confirm.");
    }
}
