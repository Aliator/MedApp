using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayBranch;

/// <summary>A branch's reason is required (at most 500 characters), as the light demo says it.</summary>
public sealed class AddPathwayBranchValidator : AbstractValidator<AddPathwayBranchCommand>
{
    public AddPathwayBranchValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");

        RuleFor(x => x.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage(PathwayBranches.ReasonRequiredMessage)
            .Must(reason => reason is null || reason.Trim().Length <= PathwayBranchLimits.MaxBranchReasonLength)
            .WithMessage(PathwayBranches.ReasonTooLongMessage);

        RuleFor(x => x.TemplateBranchId).NotEmpty().WithMessage(PathwayBranches.NotTheTemplatesMessage);
        RuleFor(x => x.AfterStageId).NotEmpty().WithMessage(PathwayBranches.ChooseAfterStageMessage);
    }
}
