using System.Globalization;
using System.Text.Json;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.Timeline.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;

namespace MedApp.Application.Procedures.Timeline.Common;

/// <summary>
/// A patient's timeline as the API returns it: audit entries read into fields (from the JSON details that
/// <see cref="ProcedureAudit"/> writes), the procedures that are due, and the procedures to filter by.
/// <para>
/// Staff see what the procedure page shows anyway (statuses, priorities, due dates, who is assigned), but never the
/// free text the audit log keeps (notes, titles, reasons), as only admins can read the log itself.
/// </para>
/// </summary>
public static class TimelineMapper
{
    /// <summary>Fields whose values everyone sees in a change; the others (free text) only admins.</summary>
    private static readonly HashSet<string> PublicValueFields = ["dueDate", "priority"];

    /// <summary>
    /// One entry. Its procedure's title is the one it has now, or, once deleted, the one it had at the time. Free-text
    /// values are left out unless <paramref name="isAdmin"/>.
    /// </summary>
    public static PatientTimelineEntryResponse ToResponse(PatientTimelineRow row, bool isAdmin)
    {
        var entry = row.Entry;
        var response = new PatientTimelineEntryResponse
        {
            Id = entry.Id,
            Kind = PatientTimelineKinds.Of(entry).ToString(),
            Action = entry.Action.ToString(),
            AtUtc = entry.AtUtc,
            Username = entry.Username,
            ProcedureId = entry.PatientProcedureId,
            ProcedureTitle = row.CurrentProcedureTitle ?? entry.ProcedureTitle,
            IsProcedureDeleted = row.CurrentProcedureTitle is null && !row.IsAboutPatient,
            StageName = entry.StageName,
        };

        var details = ParseDetails(entry.Details);
        switch (entry.Action)
        {
            case Created:
                response.TemplateName = Text(details, "template");
                response.StageCount = details.TryGetValue("stageCount", out var count)
                                      && count.ValueKind == JsonValueKind.Number
                                      && count.TryGetInt32(out var stages)
                    ? stages
                    : null;
                response.Priority = Text(details, "priority");
                response.DueDate = DateOnly.TryParseExact(Text(details, "dueDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueDate)
                    ? dueDate
                    : null;
                break;

            case StatusChanged:
                response.FromStatus = Text(details, "from");
                response.ToStatus = Text(details, "to");
                response.IsAutomatic = details.TryGetValue("automatic", out var automatic) && automatic.ValueKind == JsonValueKind.True;
                response.Reason = isAdmin ? Text(details, "reason") : null;
                break;

            case Assigned or Unassigned:
                response.AssigneeUsername = Text(details, "username");
                break;

            case StageNotesUpdated:
                response.Changes = [Change("stageNotes", details, isAdmin)];
                break;

            case DetailsUpdated when details.TryGetValue("changes", out var changes) && changes.ValueKind == JsonValueKind.Object:
                response.Changes = changes.EnumerateObject()
                    .Select(c => Change(c.Name, ObjectOf(c.Value), isAdmin || PublicValueFields.Contains(c.Name)))
                    .ToList();
                break;

            case Deleted:
                response.FromStatus = Text(details, "status");
                break;

            case AttachmentAdded or AttachmentDeleted or AttachmentDownloaded:
                response.AttachmentId = Guid.TryParse(Text(details, "attachmentId"), out var attachmentId) ? attachmentId : null;
                response.AttachedTo = Text(details, "attachedTo");
                // A file's name is free text: only admins see it, as with notes and reasons.
                response.FileName = isAdmin ? Text(details, "fileName") : null;
                break;
        }

        return response;
    }

    /// <summary>A procedure that's due, flagged when it's overdue at <paramref name="nowUtc"/>.</summary>
    public static PatientTimelineUpcomingResponse ToUpcomingResponse(PatientProcedure procedure, DateTime nowUtc) => new()
    {
        ProcedureId = procedure.Id,
        Title = procedure.Title,
        Status = procedure.Status.ToString(),
        Priority = procedure.Priority.ToString(),
        DueDate = DueDates.ToUkDate(procedure.DueDateUtc),
        IsOverdue = ProcedureWorkflow.IsOverdue(procedure, nowUtc),
    };

    /// <summary>A procedure to filter the timeline by, with the UK day it was created.</summary>
    public static PatientTimelineProcedureResponse ToProcedureResponse(PatientProcedure procedure) => new()
    {
        Id = procedure.Id,
        Title = procedure.Title,
        IsDeleted = false,
        CreatedOn = DueDates.ToUkDate(procedure.CreatedAt),
    };

    /// <summary>A deleted procedure to filter the timeline by, from the audit entry of its deletion (with its UK day).</summary>
    public static PatientTimelineProcedureResponse ToDeletedProcedureResponse(PatientProcedureAuditEntry deletion) => new()
    {
        Id = deletion.PatientProcedureId,
        Title = deletion.ProcedureTitle,
        IsDeleted = true,
        DeletedOn = DueDates.ToUkDate(deletion.AtUtc),
    };

    private static PatientTimelineChangeResponse Change(string field, IReadOnlyDictionary<string, JsonElement> fromTo, bool showValues) => new()
    {
        Field = field,
        From = showValues ? Text(fromTo, "from") : null,
        To = showValues ? Text(fromTo, "to") : null,
    };

    /// <summary>The details' top-level values by name; empty if they aren't a JSON object.</summary>
    private static IReadOnlyDictionary<string, JsonElement> ParseDetails(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ObjectOf(document.RootElement.Clone());
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    private static IReadOnlyDictionary<string, JsonElement> ObjectOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.Last().Value)
            : new Dictionary<string, JsonElement>();

    /// <summary>A value as text: a string as it is, a number or date as written, null when missing or null.</summary>
    private static string? Text(IReadOnlyDictionary<string, JsonElement> values, string name) =>
        !values.TryGetValue(name, out var value) ? null : value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.GetRawText(),
        };
}
