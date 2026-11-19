using FluentValidation;

namespace MedApp.Application.Pathways.PatientPathways.Commands.PickUpPathwayStage;

public sealed class PickUpPathwayStageValidator : AbstractValidator<PickUpPathwayStageCommand>
{
    public PickUpPathwayStageValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");
    }
}
