using MedApp.Application.Activity.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.DataRequests;

namespace MedApp.Application.DataRequests.Common;

/// <summary>What changed on a data request, as its activity entry names it (never a value).</summary>
public enum DataRequestChange
{
    Recorded,
    IdentityChecked,
    Extended,
    Reviewed,
    Sent,
    Refused,
    Withdrawn,
}

/// <summary>Bumps a data request's version and makes the activity entry recording the change.</summary>
public static class DataRequestChanges
{
    /// <summary>
    /// The entry for a change (<see cref="ActivityAction.DataRequestChanged"/>): the request's ID and what changed, never
    /// the requester, a reason or anything withheld.
    /// </summary>
    public static ActivityEntry Entry(IActivityLog activityLog, DataRequest request, DataRequestChange change) =>
        activityLog.Entry(ActivityAction.DataRequestChanged, request.PatientId, new
        {
            dataRequestId = request.Id,
            change = change.ToString()
        });

    /// <summary>Marks the request changed now: a new version, so a stale copy can't overwrite it.</summary>
    public static void Touch(DataRequest request, DateTime nowUtc)
    {
        request.Version++;
        request.LastUpdatedUtc = nowUtc;
    }
}
