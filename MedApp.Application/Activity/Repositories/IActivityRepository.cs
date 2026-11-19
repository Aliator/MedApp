using MedApp.Application.Activity.Queries.SearchActivity;
using MedApp.Application.Common.Paging;
using MedApp.Domain.Activity;

namespace MedApp.Application.Activity.Repositories;

/// <summary>The activity log. Entries are only ever added; nothing changes or deletes them, except anonymising a patient.</summary>
public interface IActivityRepository
{
    Task AddAsync(IReadOnlyCollection<ActivityEntry> entries, CancellationToken ct);

    /// <summary>
    /// Of these patients, the ones this user already has an entry for with this action (and pathway) since
    /// <paramref name="sinceUtc"/>.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetRecentlyRecordedPatientIdsAsync(
        Guid? userId,
        ActivityAction action,
        IReadOnlyCollection<Guid> patientIds,
        Guid? pathwayId,
        DateTime sinceUtc,
        CancellationToken ct);

    /// <summary>
    /// Whether this user already has an entry with this action, about this patient and with exactly these details, since
    /// <paramref name="sinceUtc"/>.
    /// </summary>
    Task<bool> HasRecentEntryAsync(
        Guid? userId,
        ActivityAction action,
        Guid? patientId,
        string details,
        DateTime sinceUtc,
        CancellationToken ct);

    /// <summary>One page of entries matching the query (see <c>ActivitySearch</c>).</summary>
    Task<PagedResult<ActivityEntry>> SearchAsync(SearchActivityQuery query, CancellationToken ct);
}
