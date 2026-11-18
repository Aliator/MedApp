using FluentValidation;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayNote;

public sealed class AddPathwayNoteValidator : AbstractValidator<AddPathwayNoteCommand>
{
    public AddPathwayNoteValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.Text).NoteText();
    }
}
