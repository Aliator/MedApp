using FluentValidation;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.AddProcedureNote;

public sealed class AddProcedureNoteValidator : AbstractValidator<AddProcedureNoteCommand>
{
    public AddProcedureNoteValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.Text).NoteText();
    }
}
