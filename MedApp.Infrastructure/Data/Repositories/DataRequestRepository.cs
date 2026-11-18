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
        var items = await DataRequestSearch.Order(query, filter)
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
        AddNewWithheldItems();
        context.ActivityEntries.Add(activity);
        await context.SaveChangesOrConflictAsync(DataRequestRules.ChangedMeanwhileMessage, ct);
    }

    /// <summary>
    /// Marks a review's new withheld items as added. Their IDs are already set, so EF's change detection would take them
    /// for existing rows and update them, which matches nothing and reads as a false "changed meanwhile" (409). Change
    /// detection is held off while they're found, so it can't do that first; the old items, left out of the list, are
    /// still deleted as orphans when the save detects changes.
    /// </summary>
    private void AddNewWithheldItems()
    {
        var detect = context.ChangeTracker.AutoDetectChangesEnabled;
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var request in context.ChangeTracker.Entries<DataRequest>().Select(e => e.Entity).ToList())
            foreach (var item in request.WithheldItems)
            {
                var entry = context.Entry(item);
                if (entry.State == EntityState.Detached)
                    entry.State = EntityState.Added;
            }
        }
        finally
        {
            context.ChangeTracker.AutoDetectChangesEnabled = detect;
        }
    }

    public async Task SaveRemindersAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct)
    {
        context.Notifications.AddRange(notifications);
        await context.SaveChangesOrConflictAsync(DataRequestRules.ChangedMeanwhileMessage, ct);
    }
}
