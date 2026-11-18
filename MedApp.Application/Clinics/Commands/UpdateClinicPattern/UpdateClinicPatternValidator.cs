using FluentValidation;
using MedApp.Application.Clinics.Common;

namespace MedApp.Application.Clinics.Commands.UpdateClinicPattern;

public sealed class UpdateClinicPatternValidator : AbstractValidator<UpdateClinicPatternCommand>
{
    public UpdateClinicPatternValidator()
    {
        this.Rules(
            x => x.StaffUserId,
            x => x.RoomId,
            x => x.SpecialtyId,
            x => x.DayOfWeek,
            x => x.Starts,
            x => x.Ends,
            x => x.SlotMinutes,
            x => x.EveryWeeks,
            x => x.FromDate,
            x => x.UntilDate);

        RuleFor(x => x.ExpectedLastUpdated).NotNull().WithMessage("Expected last updated is required.");
    }
}
