using MedApp.Application.Common.Paging;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Domain.DataRequests;

namespace MedApp.Application.DataRequests.Common;

/// <summary>
/// The data requests list's filter and order, in LINQ that runs in SQL and in memory: by default open ones first by due
/// date, then the rest, newest received first; or by a column the person chose; either way then by ID so paging never
/// repeats or skips.
/// </summary>
public static class DataRequestSearch
{
    public const int MaxSearchLength = 200;

    public const string SortByPatient = "patient";
    public const string SortByReceived = "received";
    public const string SortByDue = "due";
    public const string SortByStatus = "status";

    public static readonly IReadOnlyList<string> SortColumns = [SortByPatient, SortByReceived, SortByDue, SortByStatus];

    public static IQueryable<DataRequest> Apply(IQueryable<DataRequest> requests, DataRequestFilter filter)
    {
        if (filter.Status is { } status)
            requests = requests.Where(r => r.Status == status);
        if (filter.PatientId is { } patientId)
            requests = requests.Where(r => r.PatientId == patientId);
        if (filter.AwaitingReview)
            requests = requests.Where(r => r.Status == DataRequestStatus.Open && r.ReviewedAtUtc == null);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            requests = requests.Where(r => (r.Patient.FirstName + " " + r.Patient.LastName).ToLower().Contains(term));
        }
        return requests;
    }

    public static IQueryable<DataRequest> Order(IQueryable<DataRequest> requests, DataRequestFilter? filter = null)
    {
        var ascending = filter?.Ascending ?? true;
        var ordered = Ordering.ColumnOf(filter?.Sort, SortColumns) switch
        {
            SortByPatient => requests.OrderByDirection(r => r.Patient.LastName, ascending)
                .ThenByDirection(r => r.Patient.FirstName, ascending),
            SortByReceived => requests.OrderByDirection(r => r.ReceivedOn, ascending),
            SortByDue => requests.OrderByDirection(r => r.DueOn, ascending),
            SortByStatus => requests.OrderByDirection(r => r.Status, ascending),
            _ => requests
                .OrderBy(r => r.Status == DataRequestStatus.Open ? 0 : 1)
                .ThenBy(r => r.Status == DataRequestStatus.Open ? r.DueOn : DateOnly.MinValue)
                .ThenByDescending(r => r.ReceivedOn)
        };
        return ordered.ThenBy(r => r.Id);
    }
}
