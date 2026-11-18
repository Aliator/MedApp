using MedApp.Application.Activity.Common;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// An activity log for handler tests: makes entries as the real one does (by <see cref="Actor"/>, at
/// <see cref="Now"/>), and keeps what's recorded instead of saving it.
/// </summary>
public sealed class RecordingActivityLog : IActivityLog
{
    public ActivityActor Actor { get; set; } = ActivityTestConstants.Actor;

    public DateTime Now { get; set; } = ActivityTestConstants.Now;

    /// <summary>Entries passed to <see cref="RecordAsync"/>.</summary>
    public List<ActivityEntry> Recorded { get; } = [];

    /// <summary>Calls to <see cref="RecordViewsAsync"/>.</summary>
    public List<(ActivityAction Action, List<Guid> PatientIds, Guid? PathwayId)> Views { get; } = [];

    /// <summary>Calls to <see cref="RecordViewAsync"/>, with their details as the log would store them.</summary>
    public List<(ActivityAction Action, Guid PatientId, string Details)> SingleViews { get; } = [];

    /// <summary>The tokens every call was given.</summary>
    public List<CancellationToken> Tokens { get; } = [];

    public ActivityEntry Entry(
        ActivityAction action,
        Guid? patientId = null,
        object? details = null,
        string? targetUsername = null,
        Guid? pathwayId = null,
        ActivityActor? actor = null)
    {
        var by = actor ?? Actor;
        return new ActivityEntry
        {
            Id = Guid.NewGuid(),
            AtUtc = Now,
            Action = action,
            UserId = by.UserId,
            Username = by.Username,
            IpAddress = by.IpAddress,
            PatientId = patientId,
            PathwayId = pathwayId,
            TargetUsername = targetUsername,
            Details = ActivityLog.SerializeDetails(details)
        };
    }

    public Task RecordAsync(ActivityEntry entry, CancellationToken ct)
    {
        Recorded.Add(entry);
        Tokens.Add(ct);
        return Task.CompletedTask;
    }

    public Task RecordViewsAsync(ActivityAction action, IEnumerable<Guid> patientIds, CancellationToken ct, Guid? pathwayId = null)
    {
        Views.Add((action, patientIds.ToList(), pathwayId));
        Tokens.Add(ct);
        return Task.CompletedTask;
    }

    public Task RecordViewAsync(ActivityAction action, Guid patientId, object details, CancellationToken ct)
    {
        SingleViews.Add((action, patientId, ActivityLog.SerializeDetails(details)));
        Tokens.Add(ct);
        return Task.CompletedTask;
    }
}
