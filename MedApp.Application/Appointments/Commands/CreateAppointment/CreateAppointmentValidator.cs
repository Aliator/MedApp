using FluentValidation;
using MedApp.Application.Appointments.Common;

namespace MedApp.Application.Appointments.Commands.CreateAppointment;

public sealed class CreateAppointmentValidator : AbstractValidator<CreateAppointmentCommand>
{
    public const string BookOrRequest =
        "Give a member of staff, a start and an end to book it, or none of them to add it to Requires Appointment.";

    public CreateAppointmentValidator(TimeProvider timeProvider)
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
            .Custom((start, context) =>
            {
                var command = context.InstanceToValidate;
                if (command.IsBooking
                    && BookingTimes.WhyNot(start!.Value, command.EndUtc!.Value, timeProvider.GetUtcNow().UtcDateTime) is { } why)
                    context.AddFailure(why);
            });

        RuleFor(x => x.Notes)
            .MaximumLength(AppointmentLimits.MaxNotesLength)
            .WithMessage($"Notes must not exceed {AppointmentLimits.MaxNotesLength} characters.");
    }
}
