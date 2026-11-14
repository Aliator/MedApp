using FluentValidation;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;

public sealed class CreatePatientProcedureTemplateValidator : AbstractValidator<CreatePatientProcedureTemplateCommand>
{
    public const int MaxStages = 50;

    public CreatePatientProcedureTemplateValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(150)
            .WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.StageDefinitionIdsInOrder)
            .NotNull()
            .WithMessage("Stage definition ids are required.")
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("A template needs at least one stage.")
            .Must(ids => ids is null || ids.Count <= MaxStages)
            .WithMessage($"A template can have at most {MaxStages} stages.");

        RuleForEach(x => x.StageDefinitionIdsInOrder)
            .NotEmpty()
            .WithMessage("Stage definition id is required.");
    }
}
