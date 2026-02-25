using FluentValidation;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Commands.CreatePatientTaskStageTemplate;

public sealed class CreatePatientTaskTemplateValidator : AbstractValidator<CreatePatientTaskTemplateCommand>
{
    public CreatePatientTaskTemplateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(150)
            .WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.StageDefinitionIdsInOrder)
            .NotNull()
            .WithMessage("Stage definition ids are required.")
            .Must(x => x.Count > 0)
            .WithMessage("At least one stage definition id is required.");

        RuleForEach(x => x.StageDefinitionIdsInOrder)
            .NotEmpty()
            .WithMessage("Stage definition id is required.");
    }
}