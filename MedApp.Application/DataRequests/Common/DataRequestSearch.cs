using MedApp.Application.DataRequests.Repositories;
using MedApp.Domain.DataRequests;

namespace MedApp.Application.DataRequests.Common;

/// <summary>
/// The data requests list's filter and order, in LINQ that runs in SQL and in memory: open ones first by due date, then
/// the rest, newest received first, then by ID so paging never repeats or skips.
/// </summary>
public static class DataRequestSearch
{
    public static IQueryable<DataRequest> Apply(IQueryable<DataRequest> requests, DataRequestFilter filter)
    {
        if (filter.Status is { } status)
            requests = requests.Where(r => r.Status == status);
        if (filter.PatientId is { } patientId)
            requests = requests.Where(r => r.PatientId == patientId);
        if (filter.AwaitingReview)
            requests = requests.Where(r => r.Status == DataRequestStatus.Open && r.ReviewedAtUtc == null);
        return requests;
    }

    public static IQueryable<DataRequest> Order(IQueryable<DataRequest> requests) =>
        requests
            .OrderBy(r => r.Status == DataRequestStatus.Open ? 0 : 1)
            .ThenBy(r => r.Status == DataRequestStatus.Open ? r.DueOn : DateOnly.MinValue)
            .ThenByDescending(r => r.ReceivedOn)
            .ThenBy(r => r.Id);
}
