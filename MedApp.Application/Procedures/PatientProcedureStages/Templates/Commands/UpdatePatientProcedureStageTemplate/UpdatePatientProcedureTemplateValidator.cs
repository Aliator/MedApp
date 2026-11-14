using FluentValidation;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;

public sealed class UpdatePatientProcedureTemplateValidator : AbstractValidator<UpdatePatientProcedureTemplateCommand>
{
    public UpdatePatientProcedureTemplateValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("Template id is required.");

        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .When(x => x.Name is not null)
            .WithMessage("Name must not be blank.")
            .MaximumLength(150)
            .WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.StageDefinitionIdsInOrder)
            .Must(ids => ids is null || ids.Count > 0)
            .WithMessage("A template needs at least one stage.")
            .Must(ids => ids is null || ids.Count <= CreatePatientProcedureTemplateValidator.MaxStages)
            .WithMessage($"A template can have at most {CreatePatientProcedureTemplateValidator.MaxStages} stages.");

        RuleForEach(x => x.StageDefinitionIdsInOrder)
            .NotEmpty()
            .WithMessage("Stage definition id is required.");
    }
}
