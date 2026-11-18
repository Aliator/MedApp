using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;

public sealed class ApplyDayTemplateValidator : AbstractValidator<ApplyDayTemplateCommand>
{
    public ApplyDayTemplateValidator(WorkingDays workingDays)
    {
        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Member of staff is required.");

        // Templates don't apply on weekends or closed days, so there's nothing to choose there.
        RuleFor(x => x.Date).WorkingDay(workingDays);

        RuleFor(x => x.DayTemplateId)
            .NotEqual(Guid.Empty)
            .When(x => x.DayTemplateId is not null)
            .WithMessage("Day template id must not be empty.");
    }
}
