using System.Text.Json;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Procedures.PatientProcedures;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;

namespace MedApp.Application.Monitoring.Common;

/// <summary>
/// One thing a change to a procedure did that a monitoring rule can watch for, read from the change's audit entries.
/// </summary>
/// <param name="Event">What happened.</param>
/// <param name="ActorUsername">Who did it.</param>
/// <param name="SubjectUserId">Who a rule's person is matched against: who did it, or, when someone was assigned, who was.</param>
/// <param name="SubjectUsername">The subject's username.</param>
/// <param name="StageDefinitionId">For a stage event, the definition the stage was made from.</param>
/// <param name="StageName">For a stage event, the stage's name (copied from its definition, which admins write).</param>
/// <param name="DueDateFrom">For a due date change, the date before.</param>
/// <param name="DueDateTo">For a due date change, the date after.</param>
/// <param name="PriorityBefore">
/// The procedure's priority before the change, when the same change lowered or raised it (a details edit); rules match the
/// higher of it and the priority after, so lowering the priority in the same edit can't hide the change.
/// </param>
public sealed record MonitoredActivity(
    MonitoringEvent Event,
    string ActorUsername,
    Guid SubjectUserId,
    string SubjectUsername,
    Guid? StageDefinitionId = null,
    string? StageName = null,
    DateOnly? DueDateFrom = null,
    DateOnly? DueDateTo = null,
    PatientProcedurePriority? PriorityBefore = null)
{
    /// <summary>
    /// What these audit entries of one change to the procedure did that rules can watch for, in the order they happened.
    /// Entries no rule watches for (notes saved, a stage started, an unassignment, automatic status changes) give none.
    /// </summary>
    public static IReadOnlyList<MonitoredActivity> From(PatientProcedure procedure, IEnumerable<PatientProcedureAuditEntry> entries) =>
        entries
            .OrderBy(e => e.AtUtc)
            .Select(e => From(procedure, e))
            .OfType<MonitoredActivity>()
            .ToList();

    private static MonitoredActivity? From(PatientProcedure procedure, PatientProcedureAuditEntry entry)
    {
        switch (entry.Action)
        {
            case Action.Created:
                return ByActor(MonitoringEvent.ProcedureCreated, entry);

            case Action.StageFinished:
                return ForStage(MonitoringEvent.StageFinished, procedure, entry);

            case Action.StageUnlocked:
                return ForStage(MonitoringEvent.StageUnlocked, procedure, entry);

            case Action.StatusChanged:
                return StatusEvent(entry) is { } statusEvent ? ByActor(statusEvent, entry) : null;

            case Action.Assigned:
                using (var details = JsonDocument.Parse(entry.Details))
                {
                    var root = details.RootElement;
                    if (!root.TryGetProperty("userId", out var userId) || !userId.TryGetGuid(out var assignedId))
                        return null;

                    var assigned = root.TryGetProperty("username", out var username) ? username.GetString() ?? string.Empty : string.Empty;
                    return new MonitoredActivity(MonitoringEvent.ProcedureAssigned, entry.Username, assignedId, assigned);
                }

            case Action.DetailsUpdated:
                return DueDateChange(entry) is { } change
                    ? ByActor(MonitoringEvent.DueDateChanged, entry) with
                    {
                        DueDateFrom = change.From, DueDateTo = change.To, PriorityBefore = change.PriorityBefore
                    }
                    : null;

            default:
                return null;
        }
    }

    private static MonitoredActivity ByActor(MonitoringEvent monitoringEvent, PatientProcedureAuditEntry entry) =>
        new(monitoringEvent, entry.Username, entry.UserId, entry.Username);

    private static MonitoredActivity ForStage(MonitoringEvent monitoringEvent, PatientProcedure procedure, PatientProcedureAuditEntry entry) =>
        ByActor(monitoringEvent, entry) with
        {
            StageDefinitionId = procedure.Stages.FirstOrDefault(s => s.Id == entry.StageId)?.StageDefinitionId,
            StageName = entry.StageName
        };

    // Completed, Blocked and Cancelled are only ever chosen by someone (never automatic), as ProcedureChange writes them:
    // {"from":"…","to":"…","reason":…,"automatic":…}.
    private static MonitoringEvent? StatusEvent(PatientProcedureAuditEntry entry)
    {
        using var details = JsonDocument.Parse(entry.Details);
        if (!details.RootElement.TryGetProperty("to", out var to) || to.ValueKind != JsonValueKind.String)
            return null;

        return Enum.TryParse<PatientProcedureStatus>(to.GetString(), out var status)
            ? status switch
            {
                PatientProcedureStatus.Completed => MonitoringEvent.ProcedureCompleted,
                PatientProcedureStatus.Blocked => MonitoringEvent.ProcedureBlocked,
                PatientProcedureStatus.Cancelled => MonitoringEvent.ProcedureCancelled,
                _ => null
            }
            : null;
    }

    // A details edit that includes the due date: {"changes":{"dueDate":{"from":"2026-06-22","to":"2026-06-29"}, …}},
    // and, if the same edit changed the priority, "priority":{"from":"Urgent","to":"Low"}.
    private static (DateOnly? From, DateOnly? To, PatientProcedurePriority? PriorityBefore)? DueDateChange(PatientProcedureAuditEntry entry)
    {
        using var details = JsonDocument.Parse(entry.Details);
        if (!details.RootElement.TryGetProperty("changes", out var changes)
            || changes.ValueKind != JsonValueKind.Object
            || !changes.TryGetProperty("dueDate", out var dueDate)
            || dueDate.ValueKind != JsonValueKind.Object)
            return null;

        PatientProcedurePriority? priorityBefore = changes.TryGetProperty("priority", out var priority)
                                                   && priority.ValueKind == JsonValueKind.Object
                                                   && priority.TryGetProperty("from", out var from)
                                                   && from.ValueKind == JsonValueKind.String
                                                   && Enum.TryParse<PatientProcedurePriority>(from.GetString(), out var parsed)
            ? parsed
            : null;

        return (Date(dueDate, "from"), Date(dueDate, "to"), priorityBefore);
    }

    private static DateOnly? Date(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", out var date)
            ? date
            : null;
}
