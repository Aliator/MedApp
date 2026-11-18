using MedApp.Application.Common.Paging;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.DataRequests;
using MedApp.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Patients' data requests. Each change is saved with its activity entry in one <c>SaveChangesAsync</c>, which refuses a
/// save from a copy read before someone else's (the row version: 409).
/// </summary>
public sealed class DataRequestRepository(MedAppDbContext context) : IDataRequestRepository
{
    public Task<DataRequest?> GetAsync(Guid id, CancellationToken ct) =>
        context.DataRequests.AsNoTracking()
            .Include(d => d.Patient)
            .Include(d => d.WithheldItems)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<DataRequest?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        context.DataRequests
            .Include(d => d.Patient)
            .Include(d => d.WithheldItems)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<PagedResult<DataRequest>> SearchAsync(DataRequestFilter filter, int page, int pageSize, CancellationToken ct)
    {
        var query = DataRequestSearch.Apply(context.DataRequests.AsNoTracking(), filter);
        var total = await query.CountAsync(ct);
        var items = await DataRequestSearch.Order(query)
            .Include(d => d.Patient)
            .Skip(Paging.Skip(page, pageSize))
            .Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<DataRequest>(items, total);
    }

    public Task<int> CountForPatientAsync(Guid patientId, CancellationToken ct) =>
        context.DataRequests.CountAsync(d => d.PatientId == patientId, ct);

    public async Task<IReadOnlyList<DataRequest>> GetDueSoonUnnotifiedAsync(DateOnly dueOnOrBefore, int take, CancellationToken ct) =>
        await context.DataRequests
            .Where(d => d.Status == DataRequestStatus.Open
                        && d.DueOn <= dueOnOrBefore
                        && (d.DueSoonNotifiedFor == null || d.DueSoonNotifiedFor != d.DueOn))
            .OrderBy(d => d.DueOn).ThenBy(d => d.Id)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(DataRequest request, ActivityEntry activity, CancellationToken ct)
    {
        context.DataRequests.Add(request);
        context.ActivityEntries.Add(activity);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(ActivityEntry activity, CancellationToken ct)
    {
        context.ActivityEntries.Add(activity);
        await context.SaveChangesOrConflictAsync(DataRequestRules.ChangedMeanwhileMessage, ct);
    }

    public async Task SaveRemindersAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct)
    {
        context.Notifications.AddRange(notifications);
        await context.SaveChangesOrConflictAsync(DataRequestRules.ChangedMeanwhileMessage, ct);
    }
}
