using FluentValidation;
using MedApp.Domain.Teams;

namespace MedApp.Application.Teams.Common;

public static class TeamValidation
{
    /// <summary>A team's name: required, and at most <see cref="TeamLimits.MaxNameLength"/> characters once trimmed.</summary>
    public static IRuleBuilderOptions<T, string> TeamName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .Must(name => name is null || name.Trim().Length <= TeamLimits.MaxNameLength)
            .WithMessage($"Name must not exceed {TeamLimits.MaxNameLength} characters.");

    public static readonly string TargetDaysMessage =
        $"Each waiting-time target must be from {Team.MinTargetDays} to {Team.MaxTargetDays} days.";

    /// <summary>A waiting-time target in days, when given: from 1 to 365.</summary>
    public static IRuleBuilderOptions<T, int?> TargetDays<T>(this IRuleBuilder<T, int?> rule) =>
        rule
            .Must(days => days is null or >= Team.MinTargetDays and <= Team.MaxTargetDays)
            .WithMessage(TargetDaysMessage);

    /// <summary>
    /// Sets each target given (null leaves it as it is) and says whether any changed. They're the centre's own
    /// administrative targets, copied by clocks that start afterwards; changing them moves no running clock.
    /// </summary>
    public static bool ApplyTargets(Team team, int? urgent, int? high, int? medium, int? low)
    {
        var changed = false;
        if (urgent is { } u && u != team.TargetDaysUrgent) { team.TargetDaysUrgent = u; changed = true; }
        if (high is { } h && h != team.TargetDaysHigh) { team.TargetDaysHigh = h; changed = true; }
        if (medium is { } m && m != team.TargetDaysMedium) { team.TargetDaysMedium = m; changed = true; }
        if (low is { } l && l != team.TargetDaysLow) { team.TargetDaysLow = l; changed = true; }
        return changed;
    }
}
