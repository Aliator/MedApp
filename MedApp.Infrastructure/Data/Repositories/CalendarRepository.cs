using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;
using MedApp.Domain.Clinics;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Members of staff's calendars. Changes that put something on a calendar run inside <c>CalendarLock</c>'s transaction,
/// which this repository's queries and saves join, as they share the request's DbContext.
/// </summary>
public sealed class CalendarRepository(MedAppDbContext context) : ICalendarRepository
{
    public async Task<CalendarData> GetCalendarDataAsync(Guid staffUserId, DateOnly from, DateOnly to, CancellationToken ct) =>
        (await GetCalendarsAsync([staffUserId], from, to, ct))[0];

    public async Task<IReadOnlyList<CalendarData>> GetCalendarsAsync(
        IReadOnlyCollection<Guid> staffUserIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var ids = staffUserIds.Distinct().ToList();
        var nullableIds = ids.Select(id => (Guid?)id).ToList();

        // The range's UK days as UTC moments: from the start of the first to the start of the day after the last.
        var fromUtc = DueDates.StartOfDayUtc(from);
        var beforeUtc = DueDates.StartOfDayUtc(to.AddDays(1));

        var staffWeeks = await context.StaffWeekTemplates
            .AsNoTracking()
            .Include(s => s.WeekTemplate)
            .Where(s => ids.Contains(s.StaffUserId) && s.ValidFrom <= to && (s.ValidTo == null || s.ValidTo >= from))
            .ToListAsync(ct);

        var weekTemplateIds = staffWeeks.Select(s => s.WeekTemplateId).Distinct().ToList();
        var weekDays = await context.WeekTemplateDays
            .AsNoTracking()
            .Where(d => weekTemplateIds.Contains(d.WeekTemplateId) && d.ValidFrom <= to && (d.ValidTo == null || d.ValidTo >= from))
            .ToListAsync(ct);

        var overrides = await context.CalendarDayOverrides
            .AsNoTracking()
            .Where(o => ids.Contains(o.StaffUserId) && o.Date >= from && o.Date <= to)
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
            .Where(e => ids.Contains(e.StaffUserId) && e.Date >= from && e.Date <= to)
            .ToListAsync(ct);

        var blocks = await context.CalendarBlocks
            .AsNoTracking()
            .Include(b => b.ActivityType)
            .Where(b => ids.Contains(b.StaffUserId) && b.StartUtc < beforeUtc && b.EndUtc > fromUtc)
            .ToListAsync(ct);

        var appointments = await context.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Pathway)
            .Include(a => a.Stage)
            .Where(a => nullableIds.Contains(a.StaffUserId)
                        && (a.Status == AppointmentStatus.Booked
                            || a.Status == AppointmentStatus.Attended
                            || a.Status == AppointmentStatus.DidNotAttend)
                        && a.StartUtc < beforeUtc
                        && a.EndUtc > fromUtc)
            .AsSplitQuery()
            .ToListAsync(ct);

        var meetings = await context.Meetings
            .AsNoTracking()
            .Include(m => m.Attendees)
            .Where(m => m.Attendees.Any(a => ids.Contains(a.StaffUserId)) && m.StartUtc < beforeUtc && m.EndUtc > fromUtc)
            .AsSplitQuery()
            .ToListAsync(ct);

        var sessions = await context.ClinicSessions
            .AsNoTracking()
            .Include(c => c.Room).ThenInclude(r => r.Site)
            .Include(c => c.Specialty)
            .Where(c => ids.Contains(c.StaffUserId)
                        && c.Status == ClinicSessionStatus.Planned
                        && c.StartUtc < beforeUtc
                        && c.EndUtc > fromUtc)
            .ToListAsync(ct);

        // The range's closed days are everyone's.
        var closedDays = new ClosedDaySet(await context.ClosedDays
            .AsNoTracking()
            .Where(d => d.Date >= from && d.Date <= to)
            .ToListAsync(ct));

        // Their working hours, with their sites' (everyone's own rows are told apart by their IDs).
        var hours = await WorkingHoursRepository.LoadAsync(context, ids, from, to, ct);

        // Each person's own share of what was loaded for them all.
        return ids.Select(staffUserId =>
        {
            var theirWeeks = staffWeeks.Where(s => s.StaffUserId == staffUserId).ToList();
            var theirWeekIds = theirWeeks.Select(s => s.WeekTemplateId).ToHashSet();
            var theirDays = weekDays.Where(d => theirWeekIds.Contains(d.WeekTemplateId)).ToList();
            var theirOverrides = overrides.Where(o => o.StaffUserId == staffUserId).ToList();
            var theirDayTemplateIds = theirDays.Select(d => d.DayTemplateId)
                .Concat(theirOverrides.Where(o => o.DayTemplateId != null).Select(o => o.DayTemplateId!.Value))
                .ToHashSet();

            return new CalendarData(
                staffUserId,
                from,
                to,
                theirWeeks,
                theirDays,
                dayTemplateBlocks.Where(b => theirDayTemplateIds.Contains(b.DayTemplateId)).ToList(),
                theirOverrides,
                exceptions.Where(e => e.StaffUserId == staffUserId).ToList(),
                blocks.Where(b => b.StaffUserId == staffUserId).ToList(),
                appointments.Where(a => a.StaffUserId == staffUserId).ToList(),
                dayTemplateNames.Where(n => theirDayTemplateIds.Contains(n.Key)).ToDictionary(n => n.Key, n => n.Value))
            {
                Meetings = meetings.Where(m => m.Attendees.Any(a => a.StaffUserId == staffUserId)).ToList(),
                Sessions = sessions.Where(c => c.StaffUserId == staffUserId).ToList(),
                ClosedDays = closedDays,
                Hours = hours
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<StaffBookings>> GetBookingsOnAsync(DateOnly date, CancellationToken ct)
    {
        var fromUtc = DueDates.StartOfDayUtc(date);
        var beforeUtc = DueDates.StartOfDayUtc(date.AddDays(1));

        var appointments = await context.Appointments
            .AsNoTracking()
            .Where(a => a.StaffUserId != null
                        && (a.Status == AppointmentStatus.Booked
                            || a.Status == AppointmentStatus.Attended
                            || a.Status == AppointmentStatus.DidNotAttend)
                        && a.StartUtc < beforeUtc
                        && a.EndUtc > fromUtc)
            .GroupBy(a => a.StaffUserId!.Value)
            .Select(g => new { StaffUserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var blocks = await context.CalendarBlocks
            .AsNoTracking()
            .Where(b => b.StartUtc < beforeUtc && b.EndUtc > fromUtc)
            .GroupBy(b => b.StaffUserId)
            .Select(g => new { StaffUserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var meetings = await context.MeetingAttendees
            .AsNoTracking()
            .Where(a => context.Meetings.Any(m => m.Id == a.MeetingId && m.StartUtc < beforeUtc && m.EndUtc > fromUtc))
            .GroupBy(a => a.StaffUserId)
            .Select(g => new { StaffUserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return appointments.Select(a => a.StaffUserId)
            .Concat(blocks.Select(b => b.StaffUserId))
            .Concat(meetings.Select(m => m.StaffUserId))
            .Distinct()
            .Select(id => new StaffBookings(
                id,
                appointments.FirstOrDefault(a => a.StaffUserId == id)?.Count ?? 0,
                blocks.FirstOrDefault(b => b.StaffUserId == id)?.Count ?? 0,
                meetings.FirstOrDefault(m => m.StaffUserId == id)?.Count ?? 0))
            .ToList();
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
