using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.UpdateCalendarBlock;

public sealed class UpdateCalendarBlockValidator : AbstractValidator<UpdateCalendarBlockCommand>
{
    public UpdateCalendarBlockValidator(WorkingDays workingDays)
    {
        RuleFor(x => x.BlockId).NotEmpty().WithMessage("Block id is required.");

        RuleFor(x => x.ActivityTypeId).NotEmpty().WithMessage("Activity type is required.");

        RuleFor(x => x.StartUtc).InsideWorkingDay(x => x.EndUtc, workingDays);

        RuleFor(x => x.Note)
            .MaximumLength(CalendarLimits.MaxNoteLength)
            .WithMessage($"Note must not exceed {CalendarLimits.MaxNoteLength} characters.");
    }
}
