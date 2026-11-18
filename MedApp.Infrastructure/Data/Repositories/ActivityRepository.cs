using MedApp.Application.Activity.Queries.SearchActivity;
using MedApp.Application.Activity.Repositories;
using MedApp.Application.Common.Paging;
using MedApp.Domain.Activity;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class ActivityRepository(MedAppDbContext context) : IActivityRepository
{
    public async Task AddAsync(IReadOnlyCollection<ActivityEntry> entries, CancellationToken ct)
    {
        context.ActivityEntries.AddRange(entries);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlySet<Guid>> GetRecentlyRecordedPatientIdsAsync(
        Guid? userId,
        ActivityAction action,
        IReadOnlyCollection<Guid> patientIds,
        Guid? pathwayId,
        DateTime sinceUtc,
        CancellationToken ct)
    {
        var ids = patientIds.ToList();
        var recent = await context.ActivityEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId
                        && e.Action == action
                        && e.PathwayId == pathwayId
                        && e.AtUtc >= sinceUtc
                        && e.PatientId != null
                        && ids.Contains(e.PatientId.Value))
            .Select(e => e.PatientId!.Value)
            .Distinct()
            .ToListAsync(ct);

        return recent.ToHashSet();
    }

    public Task<bool> HasRecentEntryAsync(
        Guid? userId,
        ActivityAction action,
        Guid patientId,
        string details,
        DateTime sinceUtc,
        CancellationToken ct) =>
        context.ActivityEntries
            .AsNoTracking()
            .AnyAsync(e => e.UserId == userId
                           && e.Action == action
                           && e.PatientId == patientId
                           && e.AtUtc >= sinceUtc
                           && e.Details == details, ct);

    public async Task<PagedResult<ActivityEntry>> SearchAsync(SearchActivityQuery query, CancellationToken ct)
    {
        var filtered = context.ActivityEntries.AsNoTracking().Filter(query);

        var totalCount = await filtered.CountAsync(ct);

        var page = await filtered
            .Sort(query.Ascending)
            .Skip(Paging.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<ActivityEntry>(page, totalCount);
    }
}
