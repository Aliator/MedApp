using FluentValidation;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.DeleteProcedureNote;

public sealed class DeleteProcedureNoteValidator : AbstractValidator<DeleteProcedureNoteCommand>
{
    public DeleteProcedureNoteValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.NoteId).NotEmpty().WithMessage("Note id is required.");
        RuleFor(x => (string?)x.Reason).DeletionReason().OverridePropertyName(nameof(DeleteProcedureNoteCommand.Reason));
        RuleFor(x => x.Detail).DeletionDetail();
        RuleFor(x => x.Detail)
            .Must(detail => !string.IsNullOrWhiteSpace(detail))
            .When(x => NoteDeletionReasons.IsOther(x.Reason))
            .WithMessage(NoteDeletionReasons.OtherNeedsDetailMessage);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("The note's version is required.");
    }
}
