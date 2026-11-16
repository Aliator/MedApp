using FluentValidation;
using MedApp.Application.Appointments.Common;

namespace MedApp.Application.Appointments.Commands.UpdateAppointmentNotes;

public sealed class UpdateAppointmentNotesValidator : AbstractValidator<UpdateAppointmentNotesCommand>
{
    public UpdateAppointmentNotesValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");

        RuleFor(x => x.Notes)
            .NotNull()
            .WithMessage("Notes must not be null.")
            .MaximumLength(AppointmentLimits.MaxNotesLength)
            .WithMessage($"Notes must not exceed {AppointmentLimits.MaxNotesLength} characters.");
    }
}
