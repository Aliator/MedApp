using FluentValidation;
using MedApp.Application.Appointments.Common;

namespace MedApp.Application.Appointments.Commands.CancelAppointment;

public sealed class CancelAppointmentValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");

        RuleFor(x => x.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("A reason is required to cancel an appointment.")
            .MaximumLength(AppointmentLimits.MaxCancelReasonLength)
            .WithMessage($"Reason must not exceed {AppointmentLimits.MaxCancelReasonLength} characters.");
    }
}
