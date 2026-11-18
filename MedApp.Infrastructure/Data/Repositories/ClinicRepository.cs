using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;
using MedApp.Infrastructure.Data.Configurations.Appointments;
using MedApp.Infrastructure.Data.Configurations.Clinics;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Clinic sessions and patterns. Changes run inside <c>CalendarLock</c>'s transaction, which these queries and saves
/// join, as they share the request's DbContext; so do the appointments a session's change loads for update.
/// </summary>
public sealed class ClinicRepository(MedAppDbContext context) : IClinicRepository
{
    private static readonly AppointmentStatus[] OnCalendar =
        [AppointmentStatus.Booked, AppointmentStatus.Attended, AppointmentStatus.DidNotAttend];

    private IQueryable<ClinicSession> Sessions(bool tracked) =>
        (tracked ? context.ClinicSessions : context.ClinicSessions.AsNoTracking())
            .Include(s => s.Room).ThenInclude(r => r.Site)
            .Include(s => s.Specialty);

    private IQueryable<ClinicPattern> Patterns(bool tracked) =>
        (tracked ? context.ClinicPatterns : context.ClinicPatterns.AsNoTracking())
            .Include(p => p.Room).ThenInclude(r => r.Site)
            .Include(p => p.Specialty);

    public async Task<ClinicSession?> GetSessionAsync(Guid id, CancellationToken ct) =>
        await Sessions(false).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<ClinicSession?> GetSessionForUpdateAsync(Guid id, CancellationToken ct) =>
        await Sessions(true).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<ClinicSession>> SearchSessionsAsync(ClinicSessionFilter filter, CancellationToken ct)
    {
        var query = Sessions(false).Where(s => s.Date >= filter.From && s.Date <= filter.To);
        if (!filter.IncludeCancelled)
            query = query.Where(s => s.Status == ClinicSessionStatus.Planned);
        if (filter.StaffUserId is { } staff)
            query = query.Where(s => s.StaffUserId == staff);
        if (filter.RoomId is { } room)
            query = query.Where(s => s.RoomId == room);
        if (filter.SiteId is { } site)
            query = query.Where(s => s.Room.SiteId == site);
        if (filter.SpecialtyId is { } specialty)
            query = query.Where(s => s.SpecialtyId == specialty);

        return await query
            .OrderBy(s => s.StartUtc)
            .ThenBy(s => s.Room.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<DateTime>>> GetBookedStartsAsync(
        IEnumerable<Guid> sessionIds, CancellationToken ct)
    {
        var ids = sessionIds.Distinct().Select(id => (Guid?)id).ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<DateTime>>();

        var rows = await context.Appointments
            .AsNoTracking()
            .Where(a => ids.Contains(a.ClinicSessionId) && OnCalendar.Contains(a.Status) && a.StartUtc != null)
            .Select(a => new { SessionId = a.ClinicSessionId!.Value, Start = a.StartUtc!.Value })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.SessionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DateTime>)g.Select(r => r.Start).ToList());
    }

    public async Task<IReadOnlyList<Appointment>> GetSessionAppointmentsAsync(Guid sessionId, CancellationToken ct) =>
        await context.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => a.ClinicSessionId == sessionId && OnCalendar.Contains(a.Status))
            .OrderBy(a => a.StartUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> GetSessionAppointmentsForUpdateAsync(Guid sessionId, CancellationToken ct) =>
        await context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Pathway)
            .Include(a => a.Stage)
            .Where(a => a.ClinicSessionId == sessionId && OnCalendar.Contains(a.Status))
            .OrderBy(a => a.StartUtc)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<RoomDay?> GetRoomDayAsync(Guid roomId, DateOnly date, CancellationToken ct)
    {
        var room = await context.Rooms.AsNoTracking().Include(r => r.Site).FirstOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null)
            return null;

        var fromUtc = DueDates.StartOfDayUtc(date);
        var beforeUtc = DueDates.StartOfDayUtc(date.AddDays(1));

        var siteHours = await context.SiteHours
            .AsNoTracking()
            .Where(h => h.SiteId == room.SiteId && h.ValidFrom <= date && (h.ValidTo == null || h.ValidTo >= date))
            .ToListAsync(ct);

        var closedDays = await context.ClosedDays
            .AsNoTracking()
            .Where(d => d.Date == date && (d.SiteId == null || d.SiteId == room.SiteId))
            .ToListAsync(ct);

        var sessions = await context.ClinicSessions
            .AsNoTracking()
            .Where(s => s.RoomId == roomId
                        && s.Status == ClinicSessionStatus.Planned
                        && s.StartUtc < beforeUtc
                        && s.EndUtc > fromUtc)
            .ToListAsync(ct);

        var appointments = await context.Appointments
            .AsNoTracking()
            .Where(a => a.RoomId == roomId
                        && OnCalendar.Contains(a.Status)
                        && a.StartUtc < beforeUtc
                        && a.EndUtc > fromUtc)
            .ToListAsync(ct);

        return new RoomDay(room, date, siteHours, new ClosedDaySet(closedDays), sessions, appointments);
    }

    public async Task AddSessionAsync(ClinicSession session, CancellationToken ct)
    {
        context.ClinicSessions.Add(session);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(ClinicSessionConfiguration.PatternDateIndex))
        {
            context.Entry(session).State = EntityState.Detached;
            throw new ConflictException("Its pattern has already made a session on that date.");
        }
    }

    public async Task SaveAsync(IEnumerable<AppointmentEvent> events, string conflictMessage, CancellationToken ct)
    {
        context.AppointmentEvents.AddRange(events);
        try
        {
            await context.SaveChangesOrConflictAsync(conflictMessage, ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(AppointmentConfiguration.SessionSlotIndex))
        {
            throw new ConflictException(ClinicRules.SlotTaken);
        }
    }

    public async Task<IReadOnlySet<DateOnly>> GetPatternDatesAsync(Guid patternId, DateOnly from, DateOnly to, CancellationToken ct) =>
        (await context.ClinicSessions
            .AsNoTracking()
            .Where(s => s.PatternId == patternId && s.Date >= from && s.Date <= to)
            .Select(s => s.Date)
            .ToListAsync(ct))
        .ToHashSet();

    public async Task<IReadOnlyList<ClinicPattern>> GetPatternsAsync(CancellationToken ct) =>
        await Patterns(false)
            .OrderBy(p => p.DayOfWeek)
            .ThenBy(p => p.Starts)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

    public async Task<ClinicPattern?> GetPatternAsync(Guid id, CancellationToken ct) =>
        await Patterns(false).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<ClinicPattern?> GetPatternForUpdateAsync(Guid id, CancellationToken ct) =>
        await context.ClinicPatterns.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddPatternAsync(ClinicPattern pattern, CancellationToken ct)
    {
        context.ClinicPatterns.Add(pattern);
        await context.SaveChangesAsync(ct);
    }

    public async Task<int> CountUpcomingForStaffAsync(Guid staffUserId, DateTime nowUtc, CancellationToken ct) =>
        await context.ClinicSessions.CountAsync(
            s => s.StaffUserId == staffUserId && s.Status == ClinicSessionStatus.Planned && s.EndUtc > nowUtc, ct);
}
