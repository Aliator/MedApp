using FluentValidation;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Pathways.PatientPathways.Commands.EditPathwayNote;

public sealed class EditPathwayNoteValidator : AbstractValidator<EditPathwayNoteCommand>
{
    public EditPathwayNoteValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.NoteId).NotEmpty().WithMessage("Note id is required.");
        RuleFor(x => x.Text).NoteText();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("The note's version is required.");
    }
}
