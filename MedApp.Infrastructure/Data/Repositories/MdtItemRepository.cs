using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Domain.Calendar;
using MedApp.Domain.Mdt;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using MedApp.Domain.Teams;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>MDT items, each saved with its audit entry in one save.</summary>
public sealed class MdtItemRepository(MedAppDbContext context) : IMdtItemRepository
{
    public async Task<MdtItem?> GetAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.MdtItems.AsNoTracking()).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<MdtItem?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.MdtItems).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<MdtItem>> GetForMeetingAsync(Guid meetingId, TeamScope scope, CancellationToken ct) =>
        await WithDetails(context.MdtItems.AsNoTracking().Visible(scope, context.PathwayAccess))
            .Where(i => i.MeetingId == meetingId)
            .OrderBy(i => i.ListedAtUtc).ThenBy(i => i.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MdtItem>> GetAwaitingMeetingAsync(Guid? teamId, TeamScope scope, CancellationToken ct) =>
        await WithDetails(context.MdtItems.AsNoTracking().Visible(scope, context.PathwayAccess))
            .Where(i => i.MeetingId == null
                        && (i.Status == MdtItemStatus.Listed || i.Status == MdtItemStatus.Deferred)
                        && (teamId == null || i.TeamId == teamId))
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
        Guid? teamId, DateTime fromUtc, DateTime toUtc, TeamScope scope, CancellationToken ct)
    {
        var meetings = await context.Meetings.AsNoTracking()
            .Include(m => m.Attendees)
            .Where(m => m.IsMdt && m.TeamId != null
                        && (teamId == null || m.TeamId == teamId)
                        && m.StartUtc >= fromUtc && m.StartUtc < toUtc)
            .OrderBy(m => m.StartUtc).ThenBy(m => m.Id)
            .ToListAsync(ct);
        return await RowsAsync(meetings, scope, ct);
    }

    public async Task<MdtMeetingRow?> GetMeetingRowAsync(Guid meetingId, TeamScope scope, CancellationToken ct)
    {
        var meeting = await context.Meetings.AsNoTracking()
            .Include(m => m.Attendees)
            .FirstOrDefaultAsync(m => m.Id == meetingId && m.IsMdt && m.TeamId != null, ct);
        return meeting is null ? null : (await RowsAsync([meeting], scope, ct))[0];
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

    public Task SaveAsync(CancellationToken ct) => context.SaveChangesOrConflictAsync(MdtRules.ChangedMeanwhileMessage, ct);

    public async Task<IReadOnlyList<MdtLabWork>> GetLabWorkAsync(IReadOnlyCollection<Guid> pathwayIds, CancellationToken ct)
    {
        var ids = pathwayIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await context.Specimens.AsNoTracking()
            .Where(s => s.PathwayId != null && ids.Contains(s.PathwayId.Value) && s.Status != SpecimenStatus.EnteredInError)
            .OrderBy(s => s.TakenAtUtc).ThenBy(s => s.Id)
            .Select(s => new MdtLabWork(
                s.PathwayId!.Value,
                s.Id,
                s.Number,
                s.SpecimenType.Name,
                s.Status,
                s.ReportedAtUtc,
                context.Results
                    .Where(r => r.SpecimenId == s.Id && r.Status != ResultStatus.EnteredInError)
                    .OrderBy(r => r.ReportedAtUtc).ThenBy(r => r.Id)
                    .Select(r => (Guid?)r.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task SyncGrantsAsync(IReadOnlyCollection<MdtItem> items, DateTime nowUtc, CancellationToken ct)
    {
        var ids = items.Select(i => i.Id).ToList();
        if (ids.Count == 0)
            return;

        var live = await context.PathwayAccess
            .Where(a => a.Reason == PathwayAccessReason.Mdt && a.EndedAtUtc == null
                        && a.MdtItemId != null && ids.Contains(a.MdtItemId.Value))
            .ToListAsync(ct);
        foreach (var item in items)
            context.PathwayAccess.AddRange(MdtGrants.Sync(item, live, nowUtc));
    }

    public async Task SyncMeetingGrantsAsync(Meeting meeting, DateTime nowUtc, CancellationToken ct)
    {
        var items = await context.MdtItems.Where(i => i.MeetingId == meeting.Id).ToListAsync(ct);
        foreach (var item in items)
            item.Meeting = meeting;
        await SyncGrantsAsync(items, nowUtc, ct);
    }

    public Task<int> EndLapsedGrantsAsync(DateTime nowUtc, CancellationToken ct) =>
        context.PathwayAccess
            .Where(a => a.Reason == PathwayAccessReason.Mdt && a.EndedAtUtc == null)
            .Where(a => !context.MdtItems.Any(i =>
                i.Id == a.MdtItemId
                && i.MeetingId != null
                && i.Meeting!.IsMdt
                && i.Meeting.Attendees.Any(m => m.StaffUserId == a.UserId)
                && (i.Status == MdtItemStatus.Listed || i.Status == MdtItemStatus.Deferred || i.Meeting.EndUtc > nowUtc)))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.EndedAtUtc, nowUtc), ct);

    private static IQueryable<MdtItem> WithDetails(IQueryable<MdtItem> items) =>
        items
            .Include(i => i.Patient)
            .Include(i => i.Pathway).ThenInclude(p => p.Assignments)
            .Include(i => i.Team)
            .Include(i => i.Meeting!).ThenInclude(m => m.Attendees)
            .Include(i => i.Attendees)
            .AsSplitQuery();

    /// <summary>The meetings with their team's names and counts of the items the viewer may see.</summary>
    private async Task<IReadOnlyList<MdtMeetingRow>> RowsAsync(IReadOnlyList<Meeting> meetings, TeamScope scope, CancellationToken ct)
    {
        var ids = meetings.Select(m => m.Id).ToList();
        var teamIds = meetings.Select(m => m.TeamId!.Value).Distinct().ToList();
        var names = await context.Teams.AsNoTracking()
            .Where(s => teamIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var counts = await context.MdtItems
            .Visible(scope, context.PathwayAccess)
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
                names.GetValueOrDefault(m.TeamId!.Value) ?? string.Empty,
                counts.GetValueOrDefault(m.Id)?.Open ?? 0,
                counts.GetValueOrDefault(m.Id)?.All ?? 0))
            .ToList();
    }
}
