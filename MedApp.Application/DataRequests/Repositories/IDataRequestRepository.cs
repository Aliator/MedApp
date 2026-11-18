using MedApp.Application.Common.Paging;
using MedApp.Domain.Activity;
using MedApp.Domain.DataRequests;
using MedApp.Domain.Notifications;

namespace MedApp.Application.DataRequests.Repositories;

/// <summary>The filters of the data requests list (<see cref="IDataRequestRepository.SearchAsync"/>).</summary>
public sealed record DataRequestFilter(DataRequestStatus? Status, Guid? PatientId, bool AwaitingReview);

/// <summary>Patients' data requests. Every change is saved with its activity log entry, in one save.</summary>
public interface IDataRequestRepository
{
    /// <summary>The request with its patient and withheld items, read-only, or null.</summary>
    Task<DataRequest?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>The request with its patient and withheld items, tracked for a change, or null.</summary>
    Task<DataRequest?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>A page of requests with their patients (<see cref="DataRequestSearch"/>).</summary>
    Task<PagedResult<DataRequest>> SearchAsync(DataRequestFilter filter, int page, int pageSize, CancellationToken ct);

    /// <summary>How many requests the patient has, in any status (a patient with any can't be deleted).</summary>
    Task<int> CountForPatientAsync(Guid patientId, CancellationToken ct);

    /// <summary>
    /// Open requests due on or before <paramref name="dueOnOrBefore"/> whose "due soon" notification wasn't sent for
    /// their current due date, tracked, oldest due first, at most <paramref name="take"/>.
    /// </summary>
    Task<IReadOnlyList<DataRequest>> GetDueSoonUnnotifiedAsync(DateOnly dueOnOrBefore, int take, CancellationToken ct);

    Task AddAsync(DataRequest request, ActivityEntry activity, CancellationToken ct);

    /// <summary>Saves the changes to a request loaded for update, with its activity entry.</summary>
    Task SaveAsync(ActivityEntry activity, CancellationToken ct);

    /// <summary>Saves the reminder marks on requests loaded for update, with the notifications they sent, in one save.</summary>
    Task SaveRemindersAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct);
}
