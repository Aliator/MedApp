using FluentValidation;
using MedApp.Application.Specialties.Common;

namespace MedApp.Application.Specialties.Commands.CreateSpecialty;

public sealed class CreateSpecialtyValidator : AbstractValidator<CreateSpecialtyCommand>
{
    public CreateSpecialtyValidator()
    {
        RuleFor(x => x.Name).SpecialtyName();
        RuleFor(x => x.TargetDaysUrgent).TargetDays();
        RuleFor(x => x.TargetDaysHigh).TargetDays();
        RuleFor(x => x.TargetDaysMedium).TargetDays();
        RuleFor(x => x.TargetDaysLow).TargetDays();
    }
}
