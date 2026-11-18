using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.ChangeTemplateBlockForDay;

public sealed class ChangeTemplateBlockForDayValidator : AbstractValidator<ChangeTemplateBlockForDayCommand>
{
    public const string SameDay = "A changed template block must stay on its day.";

    public ChangeTemplateBlockForDayValidator(WorkingDays workingDays)
    {
        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Member of staff is required.");

        RuleFor(x => x.TemplateBlockId).NotEmpty().WithMessage("Template block id is required.");

        RuleFor(x => x.ActivityTypeId).NotEmpty().WithMessage("Activity type is required.");

        RuleFor(x => x.StartUtc).InsideWorkingDay(x => x.EndUtc, x => x.StaffUserId, workingDays);

        RuleFor(x => x.Note)
            .MaximumLength(CalendarLimits.MaxNoteLength)
            .WithMessage($"Note must not exceed {CalendarLimits.MaxNoteLength} characters.");

        RuleFor(x => x.StartUtc)
            .Must((x, start) => UkTime.DateOf(start) == x.Date)
            .When(x => WorkingHours.WhyBadShape(x.StartUtc, x.EndUtc) is null)
            .WithMessage(SameDay);
    }
}
