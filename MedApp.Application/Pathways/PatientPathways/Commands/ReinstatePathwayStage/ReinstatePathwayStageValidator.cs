using FluentValidation;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathways.Commands.ReinstatePathwayStage;

public sealed class ReinstatePathwayStageValidator : AbstractValidator<ReinstatePathwayStageCommand>
{
    public const string ReasonRequiredMessage = "Say why the stage is reinstated.";

    public ReinstatePathwayStageValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");

        RuleFor(x => x.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage(ReasonRequiredMessage)
            .MaximumLength(PathwayBranchLimits.MaxReinstateReasonLength)
            .WithMessage($"Reason must not exceed {PathwayBranchLimits.MaxReinstateReasonLength} characters.");
    }
}
