using FluentValidation;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;

public sealed class ChangePatientProcedureStatusValidator : AbstractValidator<ChangePatientProcedureStatusCommand>
{
    public ChangePatientProcedureStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Procedure id is required.");

        RuleFor(x => x.Status)
            .Must(ProcedureEnums.IsStatus)
            .WithMessage($"Status must be one of: {ProcedureEnums.StatusNames}.");

        RuleFor(x => x.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .When(x => ProcedureEnums.TryParse<PatientProcedureStatus>(x.Status, out var status)
                       && ProcedureWorkflow.RequiresReason(status))
            .WithMessage("A reason is required to block or cancel a procedure.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Reason must not exceed 500 characters.");
    }
}
