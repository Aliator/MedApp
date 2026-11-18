using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;

public sealed class CreateCalendarBlockValidator : AbstractValidator<CreateCalendarBlockCommand>
{
    public CreateCalendarBlockValidator(WorkingDays workingDays)
    {
        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Member of staff is required.");

        RuleFor(x => x.ActivityTypeId).NotEmpty().WithMessage("Activity type is required.");

        RuleFor(x => x.StartUtc).InsideWorkingDay(x => x.EndUtc, x => x.StaffUserId, workingDays);

        RuleFor(x => x.Note)
            .MaximumLength(CalendarLimits.MaxNoteLength)
            .WithMessage($"Note must not exceed {CalendarLimits.MaxNoteLength} characters.");
    }
}
