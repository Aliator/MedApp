using FluentValidation;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.UpdatePatientPathwayStageDefinition;

public sealed class UpdatePatientPathwayStageDefinitionValidator : AbstractValidator<UpdatePatientPathwayStageDefinitionCommand>
{
    public UpdatePatientPathwayStageDefinitionValidator()
    {
        RuleFor(x => x.StageDefinitionId)
            .NotEmpty()
            .WithMessage("Stage definition id is required.");

        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .When(x => x.Name is not null)
            .WithMessage("Name must not be blank.")
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .When(x => x.Description is not null)
            .WithMessage("Description must not be blank.")
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.Instructions)
            .NotEmpty()
            .When(x => x.Instructions is not null)
            .WithMessage("Instructions must not be blank.")
            .MaximumLength(2000)
            .WithMessage("Instructions must not exceed 2000 characters.");

        this.AddSnomedRules(x => x.SnomedCode, x => x.SnomedTerm);
        RuleFor(x => x.VisitRequirement)
            .Must(PathwayEnums.IsVisitRequirement)
            .When(x => x.VisitRequirement is not null)
            .WithMessage($"Visit requirement must be one of: {PathwayEnums.VisitRequirementNames}.");
    }
}
