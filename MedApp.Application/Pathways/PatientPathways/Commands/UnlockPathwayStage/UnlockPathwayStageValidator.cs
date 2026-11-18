using FluentValidation;

namespace MedApp.Application.Pathways.PatientPathways.Commands.UnlockPathwayStage;

public sealed class UnlockPathwayStageValidator : AbstractValidator<UnlockPathwayStageCommand>
{
    public UnlockPathwayStageValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");
    }
}
