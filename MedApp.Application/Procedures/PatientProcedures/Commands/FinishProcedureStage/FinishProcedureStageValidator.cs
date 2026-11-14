using FluentValidation;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;

public sealed class FinishProcedureStageValidator : AbstractValidator<FinishProcedureStageCommand>
{
    public FinishProcedureStageValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");
    }
}
