using FluentValidation;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;

namespace MedApp.Application.Appointments.Commands.RecordDnaFollowUp;

public sealed class RecordDnaFollowUpValidator : AbstractValidator<RecordDnaFollowUpCommand>
{
    public static readonly string DecisionNames = string.Join(", ", Enum.GetNames<DnaFollowUpDecision>());

    public const string DetailRequiredMessage =
        "Say who decided there's to be no further appointment, and why, in the detail.";

    public RecordDnaFollowUpValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");

        RuleFor(x => x.Decision)
            .Must(decision => PathwayEnums.TryParse<DnaFollowUpDecision>(decision, out _))
            .WithMessage($"Decision must be one of: {DecisionNames}.");

        RuleFor(x => x.Detail)
            .MaximumLength(AppointmentLimits.MaxDnaFollowUpDetailLength)
            .WithMessage($"Detail must not exceed {AppointmentLimits.MaxDnaFollowUpDetailLength} characters.");

        RuleFor(x => x.Detail)
            .Must(detail => !string.IsNullOrWhiteSpace(detail))
            .When(x => PathwayEnums.TryParse<DnaFollowUpDecision>(x.Decision, out var decision)
                       && decision == DnaFollowUpDecision.NoFurtherAppointment)
            .WithMessage(DetailRequiredMessage);
    }
}
