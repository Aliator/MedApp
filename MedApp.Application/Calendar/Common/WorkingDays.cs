using MedApp.Application.Calendar.Repositories;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The working day's rules (<see cref="WorkingHours"/>, <see cref="WorkingTime"/>) with the closed days and hours looked
/// up, for validators: so every command that puts something on a calendar refuses a closed day, a day the person
/// doesn't work, or a time outside their hours (400) from the same place.
/// </summary>
public sealed class WorkingDays(IClosedDayRepository closedDays, IWorkingHoursRepository hours)
{
    /// <summary>The closed days from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public async Task<ClosedDaySet> ClosedDaysAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        new(await closedDays.GetAsync(from, to, ct));

    /// <summary>A person's working day on a date: their hours, their site's, and the closed days.</summary>
    public async Task<WorkingTime> OnAsync(Guid staffUserId, DateOnly date, CancellationToken ct) =>
        (await hours.GetAsync([staffUserId], date, date, ct)).For(staffUserId, date, await ClosedDaysAsync(date, date, ct));

    /// <summary>
    /// Why something can't be at these times whoever it's for (<see cref="WorkingHours.WhyOutside(DateTime, DateTime, ClosedDaySet)"/>:
    /// its shape, the bounds and days closed everywhere), or null if it could be. For a command that names the person,
    /// use the overload that checks their day.
    /// </summary>
    public async Task<string?> WhyOutsideAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        // The times' own rules first, so a bad time doesn't cost a query.
        if (WorkingHours.WhyBadShape(startUtc, endUtc) is { } why)
            return why;

        var date = UkTime.DateOf(UkTime.Utc(startUtc));
        return WorkingHours.WhyOutside(startUtc, endUtc, await ClosedDaysAsync(date, date, ct));
    }

    /// <summary>
    /// Why something on this person's calendar can't be at these times (its shape, then their working day: closed, not
    /// one of their days, or outside their hours), or null if it can. A walk-in ignores closed days
    /// (<paramref name="closedDaysCount"/> false, decision 74).
    /// </summary>
    public async Task<string?> WhyOutsideAsync(
        Guid staffUserId, DateTime startUtc, DateTime endUtc, CancellationToken ct, bool closedDaysCount = true)
    {
        if (WorkingHours.WhyBadShape(startUtc, endUtc) is { } why)
            return why;

        var date = UkTime.DateOf(UkTime.Utc(startUtc));
        return (await OnAsync(staffUserId, date, ct)).WhyOutside(startUtc, endUtc, closedDaysCount: closedDaysCount);
    }

    /// <summary>Why a UK date can't have anything new on this person's calendar (closed, or not one of their days), or null.</summary>
    public async Task<string?> WhyNotWorkingDayAsync(Guid staffUserId, DateOnly date, CancellationToken ct) =>
        (await OnAsync(staffUserId, date, ct)).WhyNotWorking();
}
