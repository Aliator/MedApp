using FluentValidation;
using MedApp.Application.Specialties.Common;

namespace MedApp.Application.Specialties.Commands.UpdateSpecialty;

public sealed class UpdateSpecialtyValidator : AbstractValidator<UpdateSpecialtyCommand>
{
    public UpdateSpecialtyValidator()
    {
        RuleFor(x => x.SpecialtyId).NotEmpty().WithMessage("Specialty id is required.");

        RuleFor(x => x.Name).SpecialtyName();
        RuleFor(x => x.TargetDaysUrgent).TargetDays();
        RuleFor(x => x.TargetDaysHigh).TargetDays();
        RuleFor(x => x.TargetDaysMedium).TargetDays();
        RuleFor(x => x.TargetDaysLow).TargetDays();

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version is required: send the version you last saw.");
    }
}
