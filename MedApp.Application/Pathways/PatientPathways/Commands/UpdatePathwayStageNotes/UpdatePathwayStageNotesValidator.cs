using FluentValidation;

namespace MedApp.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;

public sealed class UpdatePathwayStageNotesValidator : AbstractValidator<UpdatePathwayStageNotesCommand>
{
    public UpdatePathwayStageNotesValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");

        RuleFor(x => x.Notes)
            .NotNull()
            .WithMessage("Notes must not be null.")
            .MaximumLength(4000)
            .WithMessage("Notes must not exceed 4000 characters.");
    }
}
