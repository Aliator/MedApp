using FluentValidation;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.StartProcedureStage;

public sealed class StartProcedureStageValidator : AbstractValidator<StartProcedureStageCommand>
{
    public StartProcedureStageValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");
    }
}
