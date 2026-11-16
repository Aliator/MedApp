using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;

public sealed class ApplyDayTemplateValidator : AbstractValidator<ApplyDayTemplateCommand>
{
    public ApplyDayTemplateValidator()
    {
        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Member of staff is required.");

        RuleFor(x => x.Date)
            .Must(WorkingHours.IsWorkingDay)
            .WithMessage(WorkingHours.Weekend);

        RuleFor(x => x.DayTemplateId)
            .NotEqual(Guid.Empty)
            .When(x => x.DayTemplateId is not null)
            .WithMessage("Day template id must not be empty.");
    }
}
