using FluentValidation;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;

public sealed class UpdateProcedureStageNotesValidator : AbstractValidator<UpdateProcedureStageNotesCommand>
{
    public UpdateProcedureStageNotesValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");

        RuleFor(x => x.Notes)
            .NotNull()
            .WithMessage("Notes must not be null.")
            .MaximumLength(4000)
            .WithMessage("Notes must not exceed 4000 characters.");
    }
}
