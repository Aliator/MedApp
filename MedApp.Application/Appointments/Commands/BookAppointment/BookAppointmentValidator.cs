using FluentValidation;
using MedApp.Application.Appointments.Common;

namespace MedApp.Application.Appointments.Commands.BookAppointment;

public sealed class BookAppointmentValidator : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");

        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Member of staff is required.");

        RuleFor(x => x.StartUtc)
            .Custom((start, context) =>
            {
                if (BookingTimes.WhyNot(start, context.InstanceToValidate.EndUtc, timeProvider.GetUtcNow().UtcDateTime) is { } why)
                    context.AddFailure(why);
            });
    }
}
