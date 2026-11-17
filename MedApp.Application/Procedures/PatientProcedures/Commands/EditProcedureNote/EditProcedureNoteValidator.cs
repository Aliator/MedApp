using FluentValidation;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.EditProcedureNote;

public sealed class EditProcedureNoteValidator : AbstractValidator<EditProcedureNoteCommand>
{
    public EditProcedureNoteValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.NoteId).NotEmpty().WithMessage("Note id is required.");
        RuleFor(x => x.Text).NoteText();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("The note's version is required.");
    }
}
