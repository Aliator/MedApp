using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.Infrastructure.Data.Configurations.Calendar;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>The days the clinic is closed, one everywhere per date and one per site per date (a unique index).</summary>
public sealed class ClosedDayRepository(MedAppDbContext context) : IClosedDayRepository
{
    public async Task<IReadOnlyList<ClosedDay>> GetAsync(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        await context.ClosedDays
            .AsNoTracking()
            .Where(d => (from == null || d.Date >= from) && (to == null || d.Date <= to))
            .OrderBy(d => d.Date)
            .ToListAsync(ct);

    public async Task<ClosedDay?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await context.ClosedDays.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<ClosedDay>> GetOnAsync(DateOnly date, CancellationToken ct) =>
        await context.ClosedDays.AsNoTracking().Where(d => d.Date == date).ToListAsync(ct);

    public async Task AddAsync(IReadOnlyCollection<ClosedDay> days, CancellationToken ct)
    {
        context.ClosedDays.AddRange(days);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        // Another request closed one of the dates after this one's check.
        catch (DbUpdateException e) when (e.IsViolationOf(ClosedDayConfiguration.DateAndSiteIndex))
        {
            foreach (var day in days)
                context.Entry(day).State = EntityState.Detached;

            throw new ConflictException(
                days.Count == 1
                    ? days.First().SiteId is null
                        ? $"{UkTime.DescribeDate(days.First().Date)} was made a closed day just now, by someone else."
                        : $"{UkTime.DescribeDate(days.First().Date)} was made a closed day at that site just now, by someone else."
                    : "Someone else added closed days in that year just now. Try again: those already added are skipped.");
        }
    }

    public async Task DeleteAsync(ClosedDay day, CancellationToken ct)
    {
        context.ClosedDays.Remove(day);
        await context.SaveChangesAsync(ct);
    }
}
