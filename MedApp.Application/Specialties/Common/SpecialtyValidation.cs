using FluentValidation;
using MedApp.Domain.Specialties;

namespace MedApp.Application.Specialties.Common;

public static class SpecialtyValidation
{
    /// <summary>A specialty's name: required, and at most <see cref="SpecialtyLimits.MaxNameLength"/> characters once trimmed.</summary>
    public static IRuleBuilderOptions<T, string> SpecialtyName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .Must(name => name is null || name.Trim().Length <= SpecialtyLimits.MaxNameLength)
            .WithMessage($"Name must not exceed {SpecialtyLimits.MaxNameLength} characters.");

    public static readonly string TargetDaysMessage =
        $"Each waiting-time target must be from {Specialty.MinTargetDays} to {Specialty.MaxTargetDays} days.";

    /// <summary>A waiting-time target in days, when given: from 1 to 365.</summary>
    public static IRuleBuilderOptions<T, int?> TargetDays<T>(this IRuleBuilder<T, int?> rule) =>
        rule
            .Must(days => days is null or >= Specialty.MinTargetDays and <= Specialty.MaxTargetDays)
            .WithMessage(TargetDaysMessage);

    /// <summary>
    /// Sets each target given (null leaves it as it is) and says whether any changed. They're the centre's own
    /// administrative targets, copied by clocks that start afterwards; changing them moves no running clock.
    /// </summary>
    public static bool ApplyTargets(Specialty specialty, int? urgent, int? high, int? medium, int? low)
    {
        var changed = false;
        if (urgent is { } u && u != specialty.TargetDaysUrgent) { specialty.TargetDaysUrgent = u; changed = true; }
        if (high is { } h && h != specialty.TargetDaysHigh) { specialty.TargetDaysHigh = h; changed = true; }
        if (medium is { } m && m != specialty.TargetDaysMedium) { specialty.TargetDaysMedium = m; changed = true; }
        if (low is { } l && l != specialty.TargetDaysLow) { specialty.TargetDaysLow = l; changed = true; }
        return changed;
    }
}
