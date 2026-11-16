using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;

public sealed class CreateCalendarBlockValidator : AbstractValidator<CreateCalendarBlockCommand>
{
    public CreateCalendarBlockValidator()
    {
        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Member of staff is required.");

        RuleFor(x => x.ActivityTypeId).NotEmpty().WithMessage("Activity type is required.");

        RuleFor(x => x.StartUtc)
            .Must((x, start) => WorkingHours.WhyOutside(start, x.EndUtc) is null)
            .WithMessage(x => WorkingHours.WhyOutside(x.StartUtc, x.EndUtc)!);

        RuleFor(x => x.Note)
            .MaximumLength(CalendarLimits.MaxNoteLength)
            .WithMessage($"Note must not exceed {CalendarLimits.MaxNoteLength} characters.");
    }
}
