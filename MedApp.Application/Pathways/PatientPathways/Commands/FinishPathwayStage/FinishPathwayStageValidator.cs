using FluentValidation;

namespace MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;

public sealed class FinishPathwayStageValidator : AbstractValidator<FinishPathwayStageCommand>
{
    public FinishPathwayStageValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Reason must not exceed 500 characters.");

        RuleFor(x => x.Outcome)
            .MaximumLength(Domain.Pathways.PatientPathways.PathwayBranchLimits.MaxBranchLength)
            .WithMessage($"Outcome must not exceed {Domain.Pathways.PatientPathways.PathwayBranchLimits.MaxBranchLength} characters.");
    }
}
