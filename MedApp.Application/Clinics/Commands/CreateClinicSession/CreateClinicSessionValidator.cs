using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Common;

namespace MedApp.Application.Clinics.Commands.CreateClinicSession;

public sealed class CreateClinicSessionValidator : AbstractValidator<CreateClinicSessionCommand>
{
    public CreateClinicSessionValidator(WorkingDays workingDays, TimeProvider timeProvider)
    {
        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Clinician is required.");

        RuleFor(x => x.RoomId).NotEmpty().WithMessage("Room is required.");

        RuleFor(x => x.SpecialtyId).NotEmpty().WithMessage("Specialty is required.");

        RuleFor(x => x.StartUtc).InsideWorkingDay(x => x.EndUtc, x => x.StaffUserId, workingDays);

        RuleFor(x => x.StartUtc)
            .Must(start => UkTime.Utc(start) > timeProvider.GetUtcNow().UtcDateTime)
            .WithMessage(ClinicRules.InThePast);

        RuleFor(x => x.SlotMinutes)
            .Must((x, slot) => ClinicRules.WhyBadSlots(x.StartUtc, x.EndUtc, slot) is null)
            .WithMessage(x => ClinicRules.WhyBadSlots(x.StartUtc, x.EndUtc, x.SlotMinutes));
    }
}
