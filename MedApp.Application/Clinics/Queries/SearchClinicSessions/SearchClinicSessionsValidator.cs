using FluentValidation;
using MedApp.Application.Clinics.Common;

namespace MedApp.Application.Clinics.Queries.SearchClinicSessions;

public sealed class SearchClinicSessionsValidator : AbstractValidator<SearchClinicSessionsQuery>
{
    public static readonly string RangeMessage =
        $"The dates must run forwards, over at most {ClinicLimits.MaxListDays} days.";

    public SearchClinicSessionsValidator() =>
        RuleFor(x => x.To)
            .Must((x, to) => to >= x.From && to.DayNumber - x.From.DayNumber < ClinicLimits.MaxListDays)
            .WithMessage(RangeMessage);
}
