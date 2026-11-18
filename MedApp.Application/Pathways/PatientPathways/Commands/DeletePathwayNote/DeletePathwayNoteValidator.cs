using FluentValidation;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;

public sealed class DeletePathwayNoteValidator : AbstractValidator<DeletePathwayNoteCommand>
{
    public DeletePathwayNoteValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.NoteId).NotEmpty().WithMessage("Note id is required.");
        RuleFor(x => (string?)x.Reason).DeletionReason().OverridePropertyName(nameof(DeletePathwayNoteCommand.Reason));
        RuleFor(x => x.Detail).DeletionDetail();
        RuleFor(x => x.Detail)
            .Must(detail => !string.IsNullOrWhiteSpace(detail))
            .When(x => NoteDeletionReasons.IsOther(x.Reason))
            .WithMessage(NoteDeletionReasons.OtherNeedsDetailMessage);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("The note's version is required.");
    }
}
