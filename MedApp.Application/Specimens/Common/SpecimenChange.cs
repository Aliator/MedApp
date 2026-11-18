using System.Text.Json;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using static MedApp.Domain.Specimens.SpecimenStatus;

namespace MedApp.Application.Specimens.Common;

/// <summary>
/// Applies one request's change to a specimen and writes its audit entries (<see cref="PatientPathwayAuditEntry"/>,
/// actions 16 to 23), to save in the same <c>SaveChangesAsync</c>, so the history can't miss one. It moves the
/// specimen's <see cref="Specimen.Version"/> on once per change. Handlers check first that the change is allowed
/// (<see cref="SpecimenWorkflow"/>); this only applies it.
/// <para>
/// A specimen isn't a change to its pathway, so this doesn't go through <see cref="PathwayChange"/> and doesn't move
/// the pathway's LastUpdated (decision 70), as attached files don't.
/// </para>
/// <para>
/// Details hold the specimen's ID and number, its type and lab by ID and name (admin-written), statuses, the
/// rejection reason by name, times and the requesting clinician's username; <b>never free text</b>: the site, request
/// notes and every detail typed live only on the specimen (DPIA risk 29). An edit names the free-text fields it
/// changed, without values.
/// </para>
/// </summary>
public sealed class SpecimenChange(Specimen specimen, PatientPathway pathway, PathwayActor actor, DateTime nowUtc)
{
    /// <summary>The fields an edit names when it changes them, as the audit and the timeline show them.</summary>
    public const string SpecimenTypeField = "specimenType";
    public const string LaboratoryField = "laboratory";
    public const string TakenAtField = "takenAtUtc";
    public const string RequestingUserField = "requestingUser";
    public const string SiteField = "site";
    public const string RequestNotesField = "requestNotes";

    /// <summary>The edited fields whose values everyone may see on the timeline (structured, never free text).</summary>
    public static readonly IReadOnlySet<string> StructuredFields =
        new HashSet<string> { SpecimenTypeField, LaboratoryField, TakenAtField, RequestingUserField };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<PatientPathwayAuditEntry> _auditEntries = [];

    public Specimen Specimen => specimen;

    public IReadOnlyList<PatientPathwayAuditEntry> AuditEntries => _auditEntries;

    public bool HasChanges => _auditEntries.Count > 0;

    /// <summary>Records a new specimen as taken (its version is 1).</summary>
    public void Take()
    {
        specimen.Version = 0;
        specimen.Status = Taken;
        Record(PatientPathwayAuditAction.SpecimenTaken, new { to = Taken.ToString(), takenAtUtc = specimen.TakenAtUtc });
    }

    /// <summary>
    /// Replaces its details, recording which fields changed: values for the structured ones (type, lab, taken time,
    /// requesting clinician), the free-text ones (site, request notes) by name only. Nothing is recorded if nothing
    /// changed; returns whether anything did.
    /// </summary>
    public bool EditDetails(
        SpecimenType type,
        Laboratory laboratory,
        DateTime takenAtUtc,
        string? site,
        string? requestNotes,
        Guid requestingUserId,
        string? previousRequestingUsername,
        string? requestingUsername)
    {
        var fields = new List<string>();
        var changes = new Dictionary<string, object?>();

        if (specimen.SpecimenTypeId != type.Id)
        {
            fields.Add(SpecimenTypeField);
            changes[SpecimenTypeField] = new { from = specimen.SpecimenType?.Name, to = type.Name };
            specimen.SpecimenTypeId = type.Id;
            specimen.SpecimenType = type;
        }

        if (specimen.LaboratoryId != laboratory.Id)
        {
            fields.Add(LaboratoryField);
            changes[LaboratoryField] = new { from = specimen.Laboratory?.Name, to = laboratory.Name };
            specimen.LaboratoryId = laboratory.Id;
            specimen.Laboratory = laboratory;
        }

        if (specimen.TakenAtUtc != takenAtUtc)
        {
            fields.Add(TakenAtField);
            changes[TakenAtField] = new { from = specimen.TakenAtUtc, to = takenAtUtc };
            specimen.TakenAtUtc = takenAtUtc;
        }

        if (specimen.RequestingUserId != requestingUserId)
        {
            fields.Add(RequestingUserField);
            changes[RequestingUserField] = new { from = previousRequestingUsername, to = requestingUsername };
            specimen.RequestingUserId = requestingUserId;
        }

        if (specimen.Site != site)
        {
            fields.Add(SiteField);
            specimen.Site = site;
        }

        if (specimen.RequestNotes != requestNotes)
        {
            fields.Add(RequestNotesField);
            specimen.RequestNotes = requestNotes;
        }

        if (fields.Count == 0)
            return false;

        Record(PatientPathwayAuditAction.SpecimenDetailsEdited, new { fields, changes });
        return true;
    }

    public void Send(DateTime atUtc, string? courierReference)
    {
        var from = specimen.Status;
        specimen.Status = Sent;
        specimen.SentAtUtc = atUtc;
        specimen.SentByUserId = actor.UserId;
        specimen.CourierReference = courierReference;
        Record(PatientPathwayAuditAction.SpecimenSent, new { from = from.ToString(), to = Sent.ToString(), atUtc });
    }

    public void Receive(DateTime atUtc, string? labReference)
    {
        var from = specimen.Status;
        specimen.Status = Received;
        specimen.ReceivedAtUtc = atUtc;
        specimen.ReceivedByUserId = actor.UserId;
        specimen.LabReference = labReference;
        Record(PatientPathwayAuditAction.SpecimenReceived, new { from = from.ToString(), to = Received.ToString(), atUtc });
    }

    /// <summary>Attaches its report (an attachment already made), now.</summary>
    public void Report(Guid attachmentId)
    {
        var from = specimen.Status;
        specimen.Status = Reported;
        specimen.ReportedAtUtc = nowUtc;
        specimen.ReportedByUserId = actor.UserId;
        specimen.ReportAttachmentId = attachmentId;
        Record(PatientPathwayAuditAction.SpecimenReported, new { from = from.ToString(), to = Reported.ToString(), attachmentId });
    }

    /// <summary>The lab rejected it: the reason is kept by name in the audit, its detail only on the specimen.</summary>
    public void Reject(SpecimenRejectionReason reason, string? detail)
    {
        specimen.RejectionReason = reason;
        Close(RejectedByLab, PatientPathwayAuditAction.SpecimenRejected, detail, reason);
    }

    public void MarkLost(string detail) => Close(Lost, PatientPathwayAuditAction.SpecimenLost, detail, null);

    public void MarkEnteredInError(string detail) =>
        Close(EnteredInError, PatientPathwayAuditAction.SpecimenEnteredInError, detail, null);

    private void Close(SpecimenStatus to, PatientPathwayAuditAction action, string? detail, SpecimenRejectionReason? reason)
    {
        var from = specimen.Status;
        specimen.Status = to;
        specimen.ClosedDetail = detail;
        specimen.ClosedAtUtc = nowUtc;
        specimen.ClosedByUserId = actor.UserId;
        Record(action, reason is { } r
            ? new { from = from.ToString(), to = to.ToString(), rejectionReason = r.ToString() }
            : new { from = from.ToString(), to = to.ToString() });
    }

    private void Record(PatientPathwayAuditAction action, object values)
    {
        specimen.Version++;

        // The specimen's identity first, then what this change did: every entry says which specimen, of what type,
        // at which lab (names as they are now: admin-written).
        var details = new Dictionary<string, object?>
        {
            ["specimenId"] = specimen.Id,
            ["number"] = specimen.Number,
            ["specimenTypeId"] = specimen.SpecimenTypeId,
            ["specimenType"] = specimen.SpecimenType?.Name,
            ["laboratoryId"] = specimen.LaboratoryId,
            ["laboratory"] = specimen.Laboratory?.Name,
        };
        foreach (var property in JsonSerializer.SerializeToElement(values, Json).EnumerateObject())
            details[property.Name] = property.Value;

        var stage = pathway.Stages.FirstOrDefault(s => s.Id == specimen.StageId);
        _auditEntries.Add(new PatientPathwayAuditEntry
        {
            Id = Guid.NewGuid(),
            PatientPathwayId = pathway.Id,
            PathwayTitle = pathway.Title,
            PatientId = specimen.PatientId,
            StageId = specimen.StageId,
            StageName = stage?.Name,
            Action = action,
            UserId = actor.UserId,
            Username = actor.Username,
            // One change's entries are a tick apart, so they sort in order (as PathwayChange's).
            AtUtc = nowUtc.AddTicks(_auditEntries.Count),
            Details = JsonSerializer.Serialize(details, Json),
        });
    }
}
