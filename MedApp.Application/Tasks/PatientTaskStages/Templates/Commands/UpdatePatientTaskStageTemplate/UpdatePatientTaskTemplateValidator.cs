using FluentValidation;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Commands.UpdatePatientTaskStageTemplate;

public sealed class UpdatePatientTaskTemplateValidator : AbstractValidator<UpdatePatientTaskTemplateCommand>
{
    public UpdatePatientTaskTemplateValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("Template id is required.");

        RuleFor(x => x.Name)
            .MaximumLength(150)
            .When(x => x.Name is not null)
            .WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.StageDefinitionIdsInOrder)
            .Must(x => x is null || x.Count > 0)
            .WithMessage("At least one stage definition id is required when stage definitions are provided.");

        RuleForEach(x => x.StageDefinitionIdsInOrder!)
            .NotEmpty()
            .When(x => x.StageDefinitionIdsInOrder is not null)
            .WithMessage("Stage definition id is required.");
    }
}