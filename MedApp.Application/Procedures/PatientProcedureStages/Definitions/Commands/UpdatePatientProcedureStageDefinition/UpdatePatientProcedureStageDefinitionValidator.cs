using FluentValidation;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;

public sealed class UpdatePatientProcedureStageDefinitionValidator : AbstractValidator<UpdatePatientProcedureStageDefinitionCommand>
{
    public UpdatePatientProcedureStageDefinitionValidator()
    {
        RuleFor(x => x.StageDefinitionId)
            .NotEmpty()
            .WithMessage("Stage definition id is required.");

        RuleFor(x => x.Name)
            .MaximumLength(100)
            .When(x => x.Name is not null)
            .WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null)
            .WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.Instructions)
            .MaximumLength(2000)
            .When(x => x.Instructions is not null)
            .WithMessage("Instructions must not exceed 2000 characters.");
    }
}