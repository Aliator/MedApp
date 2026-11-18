using FluentValidation;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;

namespace MedApp.Application.Appointments.Commands.BookAppointment;

public sealed class BookAppointmentValidator : AbstractValidator<BookAppointmentCommand>
{
    public const string ExpectedStatusMessage = "Expected status must be Requested (to book it) or Booked (to move it).";

    public BookAppointmentValidator(TimeProvider timeProvider, WorkingDays workingDays)
    {
        RuleFor(x => x.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");

        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Member of staff is required.");

        RuleFor(x => x.ExpectedStatus)
            .Must(status => PathwayEnums.TryParse<AppointmentStatus>(status, out var parsed) && AppointmentWorkflow.IsOpen(parsed))
            .WithMessage(ExpectedStatusMessage);

        RuleFor(x => x.StartUtc)
            .CustomAsync(async (start, context, ct) =>
            {
                var command = context.InstanceToValidate;
                if (await BookingTimes.WhyNotAsync(
                        workingDays, command.StaffUserId, start, command.EndUtc, timeProvider.GetUtcNow().UtcDateTime, ct) is { } why)
                    context.AddFailure(why);
            });
    }
}
