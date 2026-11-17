using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetAvailability;

public sealed class GetAvailabilityValidator : AbstractValidator<GetAvailabilityQuery>
{
    public const int MaxDurationMinutes = 600;

    public GetAvailabilityValidator()
    {
        RuleFor(x => x.StaffUserIds)
            .Cascade(CascadeMode.Stop)
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Choose at least one member of staff.")
            .Must(ids => ids.Distinct().Count() <= MeetingLimits.MaxPeople)
            .WithMessage($"Suggested times look at most {MeetingLimits.MaxPeople} people.");

        RuleFor(x => x.From).NotEmpty().WithMessage("From is required.");

        RuleFor(x => x.To)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("To is required.")
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("The end of the range must not be before its start.")
            .Must((x, to) => to.DayNumber - x.From.DayNumber < WorkingHours.MaxDays)
            .WithMessage($"Suggested times look at most {WorkingHours.MaxDays} days.");

        RuleFor(x => x.DurationMinutes)
            .Must(minutes => minutes > 0 && minutes <= MaxDurationMinutes && minutes % WorkingHours.GranularityMinutes == 0)
            .WithMessage($"The length must be a multiple of {WorkingHours.GranularityMinutes} minutes, at most {MaxDurationMinutes / 60} hours.");
    }
}
