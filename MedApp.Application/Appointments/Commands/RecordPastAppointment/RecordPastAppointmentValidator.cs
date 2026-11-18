using FluentValidation;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;

namespace MedApp.Application.Appointments.Commands.RecordPastAppointment;

public sealed class RecordPastAppointmentValidator : AbstractValidator<RecordPastAppointmentCommand>
{
    public RecordPastAppointmentValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Patient is required.");

        RuleFor(x => x.StageId)
            .Null()
            .When(x => x.PathwayId is null)
            .WithMessage("A stage needs its pathway.");

        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Member of staff is required.");

        RuleFor(x => x.Outcome)
            .Must(outcome => PathwayEnums.TryParse<AppointmentStatus>(outcome, out var status)
                             && AppointmentWorkflow.IsOutcome(status))
            .WithMessage($"Outcome must be one of: {AppointmentWorkflow.OutcomeNames}.");

        RuleFor(x => x.StartUtc)
            .Custom((start, context) =>
            {
                // No closed-day lookup: a walk-in may be on a closed day (decision 74).
                if (BookingTimes.WhyNotRecorded(start, context.InstanceToValidate.EndUtc, timeProvider.GetUtcNow().UtcDateTime) is { } why)
                    context.AddFailure(why);
            });

        RuleFor(x => x.Notes)
            .MaximumLength(AppointmentLimits.MaxNotesLength)
            .WithMessage($"Notes must not exceed {AppointmentLimits.MaxNotesLength} characters.");
    }
}
