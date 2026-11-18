using FluentValidation;

namespace MedApp.Application.Pathways.PatientPathways.Commands.StartPathwayStage;

public sealed class StartPathwayStageValidator : AbstractValidator<StartPathwayStageCommand>
{
    public StartPathwayStageValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");
    }
}
