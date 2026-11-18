using FluentValidation;
using MedApp.Application.Clinics.Common;

namespace MedApp.Application.Clinics.Commands.CreateClinicPattern;

public sealed class CreateClinicPatternValidator : AbstractValidator<CreateClinicPatternCommand>
{
    public CreateClinicPatternValidator() =>
        this.Rules(
            x => x.StaffUserId,
            x => x.RoomId,
            x => x.TeamId,
            x => x.DayOfWeek,
            x => x.Starts,
            x => x.Ends,
            x => x.SlotMinutes,
            x => x.EveryWeeks,
            x => x.FromDate,
            x => x.UntilDate);
}
