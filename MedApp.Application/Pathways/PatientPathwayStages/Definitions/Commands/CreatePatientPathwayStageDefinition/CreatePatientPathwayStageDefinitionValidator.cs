using FluentValidation;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.CreatePatientPathwayStageDefinition;

public sealed class CreatePatientPathwayStageDefinitionValidator : AbstractValidator<CreatePatientPathwayStageDefinitionCommand>
{
    public CreatePatientPathwayStageDefinitionValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required.")
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.Instructions)
            .NotEmpty()
            .WithMessage("Instructions are required.")
            .MaximumLength(2000)
            .WithMessage("Instructions must not exceed 2000 characters.");

        this.AddSnomedRules(x => x.SnomedCode, x => x.SnomedTerm);
        RuleFor(x => x.VisitRequirement)
            .Must(PathwayEnums.IsVisitRequirement)
            .WithMessage($"Visit requirement must be one of: {PathwayEnums.VisitRequirementNames}.");
        RuleFor(x => x.NeedsLetterKind)
            .Must(StageLetterKinds.IsValid)
            .WithMessage(StageLetterKinds.Message);
    }
}
