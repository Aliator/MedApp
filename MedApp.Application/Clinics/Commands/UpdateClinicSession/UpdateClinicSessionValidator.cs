using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Common;

namespace MedApp.Application.Clinics.Commands.UpdateClinicSession;

/// <summary>
/// The time's shape, bounds and days closed everywhere; whose session it is isn't known here, so the handler checks the
/// clinician's working day under their lock (<see cref="CalendarClashes.Find"/>).
/// </summary>
public sealed class UpdateClinicSessionValidator : AbstractValidator<UpdateClinicSessionCommand>
{
    public UpdateClinicSessionValidator(WorkingDays workingDays)
    {
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("Room is required.");

        RuleFor(x => x.SpecialtyId).NotEmpty().WithMessage("Specialty is required.");

        RuleFor(x => x.StartUtc).InsideWorkingDay(x => x.EndUtc, workingDays);

        RuleFor(x => x.SlotMinutes)
            .Must((x, slot) => ClinicRules.WhyBadSlots(x.StartUtc, x.EndUtc, slot) is null)
            .WithMessage(x => ClinicRules.WhyBadSlots(x.StartUtc, x.EndUtc, x.SlotMinutes));

        RuleFor(x => x.ExpectedLastUpdated).NotNull().WithMessage("Expected last updated is required.");
    }
}
