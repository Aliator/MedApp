using FluentValidation;
using MedApp.Application.Clinics.Common;

namespace MedApp.Application.Clinics.Commands.CancelClinicSession;

public sealed class CancelClinicSessionValidator : AbstractValidator<CancelClinicSessionCommand>
{
    public CancelClinicSessionValidator()
    {
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithMessage("Reason is required.")
            .MaximumLength(ClinicLimits.MaxCancelReasonLength)
            .WithMessage($"Reason must not exceed {ClinicLimits.MaxCancelReasonLength} characters.");

        RuleFor(x => x.ExpectedLastUpdated).NotNull().WithMessage("Expected last updated is required.");
    }
}
