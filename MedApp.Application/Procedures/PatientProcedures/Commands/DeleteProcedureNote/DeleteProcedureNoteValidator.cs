using FluentValidation;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.DeleteProcedureNote;

public sealed class DeleteProcedureNoteValidator : AbstractValidator<DeleteProcedureNoteCommand>
{
    public DeleteProcedureNoteValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.NoteId).NotEmpty().WithMessage("Note id is required.");
        RuleFor(x => x.Reason).DeletionReason();
    }
}
