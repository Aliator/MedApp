using System.Text.Json;
using System.Text.Json.Serialization;
using MedApp.Application.Activity.Repositories;
using MedApp.Domain.Activity;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Activity.Common;

public sealed class ActivityLog(
    IActivityContext context,
    IActivityRepository repository,
    TimeProvider timeProvider,
    IOptions<ActivityOptions> options)
    : IActivityLog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ActivityActor Actor => context.Actor;

    public ActivityEntry Entry(
        ActivityAction action,
        Guid? patientId = null,
        object? details = null,
        string? targetUsername = null,
        Guid? procedureId = null,
        ActivityActor? actor = null)
    {
        var by = actor ?? context.Actor;
        return new ActivityEntry
        {
            Id = Guid.NewGuid(),
            AtUtc = timeProvider.GetUtcNow().UtcDateTime,
            Action = action,
            UserId = by.UserId,
            Username = Truncate(by.Username, ActivityLimits.Username) ?? string.Empty,
            IpAddress = Truncate(by.IpAddress, ActivityLimits.IpAddress),
            PatientId = patientId,
            ProcedureId = procedureId,
            TargetUsername = Truncate(targetUsername, ActivityLimits.Username),
            Details = SerializeDetails(details)
        };
    }

    /// <summary>An entry's details as camelCase JSON, leaving out nulls; "{}" for none.</summary>
    public static string SerializeDetails(object? details) =>
        details is null ? "{}" : JsonSerializer.Serialize(details, Json);

    public Task RecordAsync(ActivityEntry entry, CancellationToken ct) =>
        repository.AddAsync([entry], ct);

    public async Task RecordViewsAsync(ActivityAction action, IEnumerable<Guid> patientIds, CancellationToken ct, Guid? procedureId = null)
    {
        var ids = patientIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        var actor = context.Actor;
        var window = TimeSpan.FromMinutes(Math.Max(0, options.Value.ViewWindowMinutes));
        var since = timeProvider.GetUtcNow().UtcDateTime - window;

        IReadOnlySet<Guid> recent = window > TimeSpan.Zero
            ? await repository.GetRecentlyRecordedPatientIdsAsync(actor.UserId, action, ids, procedureId, since, ct)
            : new HashSet<Guid>();

        var entries = ids
            .Where(id => !recent.Contains(id))
            .Select(id => Entry(action, id, procedureId: procedureId, actor: actor))
            .ToList();

        if (entries.Count > 0)
            await repository.AddAsync(entries, ct);
    }

    private static string? Truncate(string? text, int length) =>
        text is null || text.Length <= length ? text : text[..length];
}
