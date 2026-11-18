using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Domain.Calendar;
using MedApp.Domain.Mdt;
using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>MDT items, each saved with its audit entry in one save.</summary>
public sealed class MdtItemRepository(MedAppDbContext context) : IMdtItemRepository
{
    public async Task<MdtItem?> GetAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.MdtItems.AsNoTracking()).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<MdtItem?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.MdtItems).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<MdtItem>> GetForMeetingAsync(Guid meetingId, CancellationToken ct) =>
        await WithDetails(context.MdtItems.AsNoTracking())
            .Where(i => i.MeetingId == meetingId)
            .OrderBy(i => i.ListedAtUtc).ThenBy(i => i.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MdtItem>> GetAwaitingMeetingAsync(Guid? specialtyId, CancellationToken ct) =>
        await WithDetails(context.MdtItems.AsNoTracking())
            .Where(i => i.MeetingId == null
                        && (i.Status == MdtItemStatus.Listed || i.Status == MdtItemStatus.Deferred)
                        && (specialtyId == null || i.SpecialtyId == specialtyId))
            .OrderBy(i => i.ListedAtUtc).ThenBy(i => i.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MdtItem>> GetForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await WithDetails(context.MdtItems.AsNoTracking())
            .Where(i => i.PathwayId == pathwayId)
            .OrderByDescending(i => i.ListedAtUtc).ThenByDescending(i => i.Id)
            .ToListAsync(ct);

    public async Task<bool> IsOpenOnMeetingAsync(Guid pathwayId, Guid meetingId, CancellationToken ct) =>
        await context.MdtItems.AnyAsync(i => i.PathwayId == pathwayId
                                             && i.MeetingId == meetingId
                                             && (i.Status == MdtItemStatus.Listed || i.Status == MdtItemStatus.Deferred), ct);

    // Tracked, so an item moved to it keeps its key and navigation in agreement.
    public async Task<Meeting?> GetMeetingAsync(Guid meetingId, CancellationToken ct) =>
        await context.Meetings.Include(m => m.Attendees).FirstOrDefaultAsync(m => m.Id == meetingId, ct);

    public async Task<IReadOnlyList<MdtMeetingRow>> GetMeetingsAsync(
        Guid? specialtyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var meetings = await context.Meetings.AsNoTracking()
            .Include(m => m.Attendees)
            .Where(m => m.IsMdt && m.SpecialtyId != null
                        && (specialtyId == null || m.SpecialtyId == specialtyId)
                        && m.StartUtc >= fromUtc && m.StartUtc < toUtc)
            .OrderBy(m => m.StartUtc).ThenBy(m => m.Id)
            .ToListAsync(ct);
        return await RowsAsync(meetings, ct);
    }

    public async Task<MdtMeetingRow?> GetMeetingRowAsync(Guid meetingId, CancellationToken ct)
    {
        var meeting = await context.Meetings.AsNoTracking()
            .Include(m => m.Attendees)
            .FirstOrDefaultAsync(m => m.Id == meetingId && m.IsMdt && m.SpecialtyId != null, ct);
        return meeting is null ? null : (await RowsAsync([meeting], ct))[0];
    }

    public async Task<(int Open, int Discussed)> CountOnMeetingAsync(Guid meetingId, CancellationToken ct)
    {
        var counts = await context.MdtItems
            .Where(i => i.MeetingId == meetingId)
            .GroupBy(i => 1)
            .Select(g => new
            {
                Open = g.Count(i => i.Status == MdtItemStatus.Listed || i.Status == MdtItemStatus.Deferred),
                Discussed = g.Count(i => i.Status == MdtItemStatus.Discussed)
            })
            .FirstOrDefaultAsync(ct);
        return counts is null ? (0, 0) : (counts.Open, counts.Discussed);
    }

    public async Task AddAsync(MdtItem item, PatientPathwayAuditEntry audit, CancellationToken ct)
    {
        context.MdtItems.Add(item);
        context.PatientPathwayAuditEntries.Add(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(PatientPathwayAuditEntry audit, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.Add(audit);
        await context.SaveChangesOrConflictAsync(MdtRules.ChangedMeanwhileMessage, ct);
    }

    private static IQueryable<MdtItem> WithDetails(IQueryable<MdtItem> items) =>
        items
            .Include(i => i.Patient)
            .Include(i => i.Pathway).ThenInclude(p => p.Assignments)
            .Include(i => i.Specialty)
            .Include(i => i.Meeting!).ThenInclude(m => m.Attendees)
            .Include(i => i.Attendees)
            .AsSplitQuery();

    private async Task<IReadOnlyList<MdtMeetingRow>> RowsAsync(IReadOnlyList<Meeting> meetings, CancellationToken ct)
    {
        var ids = meetings.Select(m => m.Id).ToList();
        var specialtyIds = meetings.Select(m => m.SpecialtyId!.Value).Distinct().ToList();
        var names = await context.Specialties.AsNoTracking()
            .Where(s => specialtyIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var counts = await context.MdtItems
            .Where(i => i.MeetingId != null && ids.Contains(i.MeetingId.Value))
            .GroupBy(i => i.MeetingId!.Value)
            .Select(g => new
            {
                g.Key,
                All = g.Count(),
                Open = g.Count(i => i.Status == MdtItemStatus.Listed || i.Status == MdtItemStatus.Deferred)
            })
            .ToDictionaryAsync(x => x.Key, ct);

        return meetings
            .Select(m => new MdtMeetingRow(
                m,
                names.GetValueOrDefault(m.SpecialtyId!.Value) ?? string.Empty,
                counts.GetValueOrDefault(m.Id)?.Open ?? 0,
                counts.GetValueOrDefault(m.Id)?.All ?? 0))
            .ToList();
    }
}
