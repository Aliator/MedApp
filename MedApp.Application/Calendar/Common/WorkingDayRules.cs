using FluentValidation;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The working day's rule for validators (<see cref="WorkingDays"/>): one working day (not a weekend or a closed day),
/// 08:00 to 18:00 UK time, on 5-minute boundaries. Every command that puts something on a calendar uses it, so each
/// refuses a closed day (400) as it refuses a weekend.
/// </summary>
public static class WorkingDayRules
{
    /// <summary>The start, with the end <paramref name="end"/> gives, must be inside one working day.</summary>
    public static IRuleBuilderOptionsConditions<T, DateTime> InsideWorkingDay<T>(
        this IRuleBuilder<T, DateTime> rule, Func<T, DateTime> end, WorkingDays workingDays) =>
        rule.CustomAsync(async (start, context, ct) =>
        {
            if (await workingDays.WhyOutsideAsync(start, end(context.InstanceToValidate), ct) is { } why)
                context.AddFailure(why);
        });

    /// <summary>The date must be a working day (not a weekend or a closed day).</summary>
    public static IRuleBuilderOptionsConditions<T, DateOnly> WorkingDay<T>(
        this IRuleBuilder<T, DateOnly> rule, WorkingDays workingDays) =>
        rule.CustomAsync(async (date, context, ct) =>
        {
            if (await workingDays.WhyNotWorkingDayAsync(date, ct) is { } why)
                context.AddFailure(why);
        });
}
