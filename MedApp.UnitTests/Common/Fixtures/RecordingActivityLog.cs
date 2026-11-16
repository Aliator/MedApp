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
    public List<(ActivityAction Action, List<Guid> PatientIds, Guid? ProcedureId)> Views { get; } = [];

    /// <summary>The tokens every call was given.</summary>
    public List<CancellationToken> Tokens { get; } = [];

    public ActivityEntry Entry(
        ActivityAction action,
        Guid? patientId = null,
        object? details = null,
        string? targetUsername = null,
        Guid? procedureId = null,
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
            ProcedureId = procedureId,
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

    public Task RecordViewsAsync(ActivityAction action, IEnumerable<Guid> patientIds, CancellationToken ct, Guid? procedureId = null)
    {
        Views.Add((action, patientIds.ToList(), procedureId));
        Tokens.Add(ct);
        return Task.CompletedTask;
    }
}
