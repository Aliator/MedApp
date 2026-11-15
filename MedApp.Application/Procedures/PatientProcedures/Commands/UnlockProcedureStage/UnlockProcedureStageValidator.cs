using FluentValidation;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UnlockProcedureStage;

public sealed class UnlockProcedureStageValidator : AbstractValidator<UnlockProcedureStageCommand>
{
    public UnlockProcedureStageValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Stage id is required.");
    }
}
