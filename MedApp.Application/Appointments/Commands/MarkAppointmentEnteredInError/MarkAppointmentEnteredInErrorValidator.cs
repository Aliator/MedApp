using FluentValidation;
using MedApp.Application.Appointments.Common;

namespace MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;

public sealed class MarkAppointmentEnteredInErrorValidator : AbstractValidator<MarkAppointmentEnteredInErrorCommand>
{
    public MarkAppointmentEnteredInErrorValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");

        RuleFor(x => x.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("A reason is required to mark an appointment entered in error.")
            .MaximumLength(AppointmentLimits.MaxCancelReasonLength)
            .WithMessage($"Reason must not exceed {AppointmentLimits.MaxCancelReasonLength} characters.");
    }
}
