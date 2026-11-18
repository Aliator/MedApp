using MedApp.Application.Calendar.Repositories;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The working day's rules (<see cref="WorkingHours"/>) with the closed days looked up, for validators: so every
/// command that puts something on a calendar refuses a closed day (400) from the same place it refuses a weekend.
/// </summary>
public sealed class WorkingDays(IClosedDayRepository closedDays)
{
    /// <summary>The closed days from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public async Task<ClosedDaySet> ClosedDaysAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        new(await closedDays.GetAsync(from, to, ct));

    /// <summary>Why something can't be at these times (<see cref="WorkingHours.WhyOutside(DateTime, DateTime, ClosedDaySet)"/>), or null if it can.</summary>
    public async Task<string?> WhyOutsideAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        // The times' own rules first, so a bad time doesn't cost a query.
        if (WorkingHours.WhyOutside(startUtc, endUtc, ClosedDaySet.None) is { } why)
            return why;

        var date = UkTime.DateOf(UkTime.Utc(startUtc));
        return WorkingHours.WhyOutside(startUtc, endUtc, await ClosedDaysAsync(date, date, ct));
    }

    /// <summary>Why a UK date can't have anything new on it (a weekend or a closed day), or null if it can.</summary>
    public async Task<string?> WhyNotWorkingDayAsync(DateOnly date, CancellationToken ct) =>
        WorkingHours.IsWorkingDay(date)
            ? WorkingHours.WhyNotWorkingDay(date, await ClosedDaysAsync(date, date, ct))
            : WorkingHours.Weekend;
}
