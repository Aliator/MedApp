using FluentValidation;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UnassignPatientProcedure;

public sealed class UnassignPatientProcedureValidator : AbstractValidator<UnassignPatientProcedureCommand>
{
    public UnassignPatientProcedureValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User id is required.");
    }
}
