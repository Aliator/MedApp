using FluentValidation;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;

public sealed class CreatePatientProcedureStageDefinitionValidator : AbstractValidator<CreatePatientProcedureStageDefinitionCommand>
{
    public CreatePatientProcedureStageDefinitionValidator()
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
    }
}
