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
    }
}
