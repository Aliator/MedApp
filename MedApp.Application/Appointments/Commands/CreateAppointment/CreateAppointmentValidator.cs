using FluentValidation;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Appointments.Commands.CreateAppointment;

public sealed class CreateAppointmentValidator : AbstractValidator<CreateAppointmentCommand>
{
    public const string BookOrRequest =
        "Give a member of staff, a start and an end to book it, or none of them to add it to Needs booking.";

    public CreateAppointmentValidator(TimeProvider timeProvider, WorkingDays workingDays)
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Patient is required.");

        RuleFor(x => x.StageId)
            .Null()
            .When(x => x.ProcedureId is null)
            .WithMessage("A stage needs its procedure.");

        RuleFor(x => x.StaffUserId)
            .Must((x, _) => x.IsBooking || x.IsRequest)
            .WithMessage(BookOrRequest);

        RuleFor(x => x.StaffUserId)
            .NotEqual(Guid.Empty)
            .When(x => x.StaffUserId is not null)
            .WithMessage("Member of staff is required.");

        RuleFor(x => x.StartUtc)
            .CustomAsync(async (start, context, ct) =>
            {
                var command = context.InstanceToValidate;
                if (command.IsBooking
                    && await BookingTimes.WhyNotAsync(
                        workingDays, start!.Value, command.EndUtc!.Value, timeProvider.GetUtcNow().UtcDateTime, ct) is { } why)
                    context.AddFailure(why);
            });

        RuleFor(x => x.Notes)
            .MaximumLength(AppointmentLimits.MaxNotesLength)
            .WithMessage($"Notes must not exceed {AppointmentLimits.MaxNotesLength} characters.");
    }
}
