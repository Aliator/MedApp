using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendar;

public sealed class GetCalendarValidator : AbstractValidator<GetCalendarQuery>
{
    public GetCalendarValidator()
    {
        RuleFor(x => x.From).NotEmpty().WithMessage("From is required.");

        RuleFor(x => x.To)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("To is required.")
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("The end of the range must not be before its start.")
            .Must((x, to) => to.DayNumber - x.From.DayNumber < WorkingHours.MaxDays)
            .WithMessage($"A calendar shows at most {WorkingHours.MaxDays} days.");
    }
}
