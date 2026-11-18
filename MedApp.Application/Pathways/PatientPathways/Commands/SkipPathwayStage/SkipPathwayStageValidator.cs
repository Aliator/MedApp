using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathways.Commands.SkipPathwayStage;

public sealed class SkipPathwayStageValidator : AbstractValidator<SkipPathwayStageCommand>
{
    public const string ReasonMessage = "Reason must be NotNeeded, PatientDeclined or Other.";
    public const string OtherNeedsDetailMessage = "Say why the stage is skipped when the reason is Other.";

    public SkipPathwayStageValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");

        // BranchNotChosen is the app's, when another branch is chosen; never given by hand.
        RuleFor(x => x.Reason)
            .Must(reason => StageSkipReasons.TryParseChosen(reason, out _))
            .WithMessage(ReasonMessage);

        RuleFor(x => x.Detail)
            .MaximumLength(PathwayBranchLimits.MaxSkipDetailLength)
            .WithMessage($"Detail must not exceed {PathwayBranchLimits.MaxSkipDetailLength} characters.");

        RuleFor(x => x.Detail)
            .Must(detail => !string.IsNullOrWhiteSpace(detail))
            .When(x => StageSkipReasons.TryParseChosen(x.Reason, out var reason) && reason == StageSkipReason.Other)
            .WithMessage(OtherNeedsDetailMessage);
    }
}
