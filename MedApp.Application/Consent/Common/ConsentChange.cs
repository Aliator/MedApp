using System.Text.Json;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Consent.Common;

/// <summary>
/// Applies one request's change to a consent record and writes its audit entries (<see cref="PatientPathwayAuditEntry"/>,
/// actions 42 to 44), to save in the same <c>SaveChangesAsync</c>, so the history can't miss one. It moves the record's
/// <see cref="ConsentRecord.Version"/> on once per change. Handlers check first that the change is allowed
/// (<see cref="ConsentWorkflow"/>); this only applies it.
/// <para>
/// Consent isn't a change to its pathway, so this doesn't go through <see cref="PathwayChange"/> and doesn't move the
/// pathway's LastUpdated (decision 70), as specimens and files don't.
/// </para>
/// <para>
/// Details hold the record's ID, the form by ID, name and version (admin-written), who signed by name, the date signed,
/// the status and whether there's a scan (its file's ID); <b>never free text</b>: a withdrawal's or correction's detail
/// lives only on the record (DPIA risk 35).
/// </para>
/// </summary>
public sealed class ConsentChange(ConsentRecord record, PatientPathway pathway, PathwayActor actor, DateTime nowUtc)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<PatientPathwayAuditEntry> _auditEntries = [];

    public ConsentRecord Record => record;

    public IReadOnlyList<PatientPathwayAuditEntry> AuditEntries => _auditEntries;

    /// <summary>Records new consent as Valid (its version is 1).</summary>
    public void Recorded()
    {
        record.Version = 0;
        record.Status = ConsentStatus.Valid;
        Audit(PatientPathwayAuditAction.ConsentRecorded, new { to = ConsentStatus.Valid.ToString() });
    }

    public void Withdraw(DateOnly withdrawnOn, string detail)
    {
        var from = record.Status;
        record.Status = ConsentStatus.Withdrawn;
        record.WithdrawnOn = withdrawnOn;
        Close(detail);
        Audit(PatientPathwayAuditAction.ConsentWithdrawn, new { from = from.ToString(), to = ConsentStatus.Withdrawn.ToString(), withdrawnOn });
    }

    public void MarkEnteredInError(string detail)
    {
        var from = record.Status;
        record.Status = ConsentStatus.EnteredInError;
        Close(detail);
        Audit(PatientPathwayAuditAction.ConsentEnteredInError, new { from = from.ToString(), to = ConsentStatus.EnteredInError.ToString() });
    }

    /// <summary>Adds the scan to a record that had none (no audit entry of its own: the file's own AttachmentAdded is the history).</summary>
    public void AttachScan(Guid attachmentId)
    {
        record.ScanAttachmentId = attachmentId;
        record.Version++;
    }

    private void Close(string detail)
    {
        record.StatusDetail = detail;
        record.StatusChangedAtUtc = nowUtc;
        record.StatusChangedByUserId = actor.UserId;
    }

    private void Audit(PatientPathwayAuditAction action, object values)
    {
        record.Version++;

        var details = new Dictionary<string, object?>
        {
            ["consentRecordId"] = record.Id,
            ["consentFormId"] = record.ConsentFormId,
            ["formName"] = record.FormName,
            ["formVersion"] = record.FormVersion,
            ["signedBy"] = record.SignedBy.ToString(),
            ["signedOn"] = record.SignedOn,
            ["scanAttachmentId"] = record.ScanAttachmentId,
        };
        foreach (var property in JsonSerializer.SerializeToElement(values, Json).EnumerateObject())
            details[property.Name] = property.Value;

        var stage = pathway.Stages.FirstOrDefault(s => s.Id == record.StageId);
        _auditEntries.Add(new PatientPathwayAuditEntry
        {
            Id = Guid.NewGuid(),
            PatientPathwayId = pathway.Id,
            PathwayTitle = pathway.Title,
            PatientId = record.PatientId,
            TeamId = record.TeamId,
            StageId = record.StageId,
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
