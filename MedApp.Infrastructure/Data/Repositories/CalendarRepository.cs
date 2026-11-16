using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Members of staff's calendars. Changes that put something on a calendar run inside <c>CalendarLock</c>'s transaction,
/// which this repository's queries and saves join, as they share the request's DbContext.
/// </summary>
public sealed class CalendarRepository(MedAppDbContext context) : ICalendarRepository
{
    public async Task<CalendarData> GetCalendarDataAsync(Guid staffUserId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        // The range's UK days as UTC moments: from the start of the first to the start of the day after the last.
        var fromUtc = DueDates.StartOfDayUtc(from);
        var beforeUtc = DueDates.StartOfDayUtc(to.AddDays(1));

        var staffWeeks = await context.StaffWeekTemplates
            .AsNoTracking()
            .Include(s => s.WeekTemplate)
            .Where(s => s.StaffUserId == staffUserId && s.ValidFrom <= to && (s.ValidTo == null || s.ValidTo >= from))
            .ToListAsync(ct);

        var weekTemplateIds = staffWeeks.Select(s => s.WeekTemplateId).Distinct().ToList();
        var weekDays = await context.WeekTemplateDays
            .AsNoTracking()
            .Where(d => weekTemplateIds.Contains(d.WeekTemplateId) && d.ValidFrom <= to && (d.ValidTo == null || d.ValidTo >= from))
            .ToListAsync(ct);

        var overrides = await context.CalendarDayOverrides
            .AsNoTracking()
            .Where(o => o.StaffUserId == staffUserId && o.Date >= from && o.Date <= to)
            .ToListAsync(ct);

        var dayTemplateIds = weekDays.Select(d => d.DayTemplateId)
            .Concat(overrides.Where(o => o.DayTemplateId != null).Select(o => o.DayTemplateId!.Value))
            .Distinct()
            .ToList();

        var dayTemplateBlocks = await context.DayTemplateBlocks
            .AsNoTracking()
            .Include(b => b.ActivityType)
            .Where(b => dayTemplateIds.Contains(b.DayTemplateId) && b.ValidFrom <= to && (b.ValidTo == null || b.ValidTo >= from))
            .ToListAsync(ct);

        var dayTemplateNames = await context.DayTemplates
            .AsNoTracking()
            .Where(t => dayTemplateIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var exceptions = await context.CalendarBlockExceptions
            .AsNoTracking()
            .Where(e => e.StaffUserId == staffUserId && e.Date >= from && e.Date <= to)
            .ToListAsync(ct);

        var blocks = await context.CalendarBlocks
            .AsNoTracking()
            .Include(b => b.ActivityType)
            .Where(b => b.StaffUserId == staffUserId && b.StartUtc < beforeUtc && b.EndUtc > fromUtc)
            .ToListAsync(ct);

        var appointments = await context.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Procedure)
            .Include(a => a.Stage)
            .Where(a => a.StaffUserId == staffUserId
                        && (a.Status == AppointmentStatus.Booked
                            || a.Status == AppointmentStatus.Attended
                            || a.Status == AppointmentStatus.DidNotAttend)
                        && a.StartUtc < beforeUtc
                        && a.EndUtc > fromUtc)
            .AsSplitQuery()
            .ToListAsync(ct);

        return new CalendarData(
            staffUserId,
            from,
            to,
            staffWeeks,
            weekDays,
            dayTemplateBlocks,
            overrides,
            exceptions,
            blocks,
            appointments,
            dayTemplateNames);
    }

    public async Task<IReadOnlyList<StaffWeekTemplate>> GetStaffWeekTemplatesOnAsync(DateOnly date, CancellationToken ct) =>
        await context.StaffWeekTemplates
            .AsNoTracking()
            .Include(s => s.WeekTemplate)
            .Where(s => s.ValidFrom <= date && (s.ValidTo == null || s.ValidTo >= date))
            .ToListAsync(ct);

    public async Task<StaffWeekTemplate?> GetStaffWeekTemplateOnAsync(Guid staffUserId, DateOnly date, CancellationToken ct) =>
        await context.StaffWeekTemplates
            .AsNoTracking()
            .Include(s => s.WeekTemplate)
            .Where(s => s.StaffUserId == staffUserId && s.ValidFrom <= date && (s.ValidTo == null || s.ValidTo >= date))
            .OrderByDescending(s => s.ValidFrom)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<StaffWeekTemplate>> GetStaffWeekTemplatesForUpdateAsync(
        Guid staffUserId, DateOnly today, CancellationToken ct) =>
        await context.StaffWeekTemplates
            .Where(s => s.StaffUserId == staffUserId && (s.ValidTo == null || s.ValidTo >= today))
            .ToListAsync(ct);

    public async Task SaveStaffWeekTemplatesAsync(TemplateHistoryChange<StaffWeekTemplate> change, CancellationToken ct)
    {
        CalendarSetupRepository.Apply(context.StaffWeekTemplates, change);
        await context.SaveChangesAsync(ct);
    }

    public async Task<CalendarBlock?> GetBlockAsync(Guid id, CancellationToken ct) =>
        await context.CalendarBlocks
            .AsNoTracking()
            .Include(b => b.ActivityType)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<CalendarBlock?> GetBlockForUpdateAsync(Guid id, CancellationToken ct) =>
        await context.CalendarBlocks.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task AddBlockAsync(CalendarBlock block, CancellationToken ct)
    {
        context.CalendarBlocks.Add(block);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteBlockAsync(CalendarBlock block, CancellationToken ct)
    {
        context.CalendarBlocks.Remove(block);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddBlockExceptionAsync(CalendarBlockException exception, CalendarBlock? replacement, CancellationToken ct)
    {
        context.CalendarBlockExceptions.Add(exception);
        if (replacement is not null)
            context.CalendarBlocks.Add(replacement);

        await context.SaveChangesAsync(ct);
    }

    public async Task<CalendarDayOverride?> GetDayOverrideForUpdateAsync(Guid staffUserId, DateOnly date, CancellationToken ct) =>
        await context.CalendarDayOverrides.FirstOrDefaultAsync(o => o.StaffUserId == staffUserId && o.Date == date, ct);

    public async Task AddDayOverrideAsync(CalendarDayOverride dayOverride, CancellationToken ct)
    {
        context.CalendarDayOverrides.Add(dayOverride);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteDayOverrideAsync(CalendarDayOverride dayOverride, CancellationToken ct)
    {
        context.CalendarDayOverrides.Remove(dayOverride);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(CancellationToken ct) => await context.SaveChangesAsync(ct);
}
