using FluentValidation;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The working day's rule for validators (<see cref="WorkingDays"/>): one day, on 5-minute boundaries, inside the
/// person's working day (their hours, their site's, not a closed day). Every command that puts something on a calendar
/// uses it, so each refuses a closed day (400) as it refuses a day the person doesn't work.
/// </summary>
public static class WorkingDayRules
{
    /// <summary>
    /// The start, with the end <paramref name="end"/> gives, must be inside one day anyone could work: the shape, the
    /// bounds and days closed everywhere. For a command whose validator can't know the person (a moved block, a
    /// meeting's attendees); the handler checks their day under the calendar lock (<see cref="CalendarClashes.Find"/>).
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, DateTime> InsideWorkingDay<T>(
        this IRuleBuilder<T, DateTime> rule, Func<T, DateTime> end, WorkingDays workingDays) =>
        rule.CustomAsync(async (start, context, ct) =>
        {
            if (await workingDays.WhyOutsideAsync(start, end(context.InstanceToValidate), ct) is { } why)
                context.AddFailure(why);
        });

    /// <summary>The start, with the end <paramref name="end"/> gives, must be inside the working day of the person <paramref name="staffUserId"/> gives.</summary>
    public static IRuleBuilderOptionsConditions<T, DateTime> InsideWorkingDay<T>(
        this IRuleBuilder<T, DateTime> rule, Func<T, DateTime> end, Func<T, Guid> staffUserId, WorkingDays workingDays) =>
        rule.CustomAsync(async (start, context, ct) =>
        {
            var command = context.InstanceToValidate;
            var why = staffUserId(command) == Guid.Empty
                ? await workingDays.WhyOutsideAsync(start, end(command), ct)
                : await workingDays.WhyOutsideAsync(staffUserId(command), start, end(command), ct);
            if (why is not null)
                context.AddFailure(why);
        });

    /// <summary>The date must be one the person <paramref name="staffUserId"/> gives works (one of their days, not closed).</summary>
    public static IRuleBuilderOptionsConditions<T, DateOnly> WorkingDay<T>(
        this IRuleBuilder<T, DateOnly> rule, Func<T, Guid> staffUserId, WorkingDays workingDays) =>
        rule.CustomAsync(async (date, context, ct) =>
        {
            var staff = staffUserId(context.InstanceToValidate);
            if (staff != Guid.Empty && await workingDays.WhyNotWorkingDayAsync(staff, date, ct) is { } why)
                context.AddFailure(why);
        });
}
