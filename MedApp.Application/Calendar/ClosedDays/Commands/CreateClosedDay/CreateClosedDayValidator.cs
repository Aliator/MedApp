using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;

public sealed class CreateClosedDayValidator : AbstractValidator<CreateClosedDayCommand>
{
    public CreateClosedDayValidator()
    {
        RuleFor(x => x.Date)
            .NotEqual(default(DateOnly))
            .WithMessage("Date is required.");

        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(CalendarLimits.MaxClosedDayNameLength)
            .WithMessage($"Name must not exceed {CalendarLimits.MaxClosedDayNameLength} characters.");
    }
}
