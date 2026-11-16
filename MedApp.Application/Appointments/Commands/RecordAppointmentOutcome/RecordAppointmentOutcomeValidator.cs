using FluentValidation;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;

namespace MedApp.Application.Appointments.Commands.RecordAppointmentOutcome;

public sealed class RecordAppointmentOutcomeValidator : AbstractValidator<RecordAppointmentOutcomeCommand>
{
    public RecordAppointmentOutcomeValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");

        RuleFor(x => x.Outcome)
            .Must(outcome => ProcedureEnums.TryParse<AppointmentStatus>(outcome, out var status)
                             && AppointmentWorkflow.IsOutcome(status))
            .WithMessage($"Outcome must be one of: {AppointmentWorkflow.OutcomeNames}.");
    }
}
