using FluentValidation;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;

public sealed class AssignPatientProcedureValidator : AbstractValidator<AssignPatientProcedureCommand>
{
    public AssignPatientProcedureValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User id is required.");
    }
}
