using FluentValidation;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;

public sealed class SetStaffWeekTemplateValidator : AbstractValidator<SetStaffWeekTemplateCommand>
{
    public SetStaffWeekTemplateValidator()
    {
        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Member of staff is required.");

        RuleFor(x => x.WeekTemplateId)
            .NotEqual(Guid.Empty)
            .When(x => x.WeekTemplateId is not null)
            .WithMessage("Week template id must not be empty.");
    }
}
