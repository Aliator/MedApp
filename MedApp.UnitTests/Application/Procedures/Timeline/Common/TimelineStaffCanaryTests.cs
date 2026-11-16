using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Attachments;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.Timeline.Common;

/// <summary>
/// A canary for the timeline's rule that staff never get free text from the audit log: every action is recorded the
/// way handlers record it, with one unique string as every piece of free text (stage notes, procedure notes, titles,
/// reasons and file names). The whole staff response, serialised as the API sends it, must never hold it, whatever fields are
/// added later; an admin's must.
/// </summary>
[TestFixture]
public sealed class TimelineStaffCanaryTests
{
    private const string Canary = "canary-text-7f3a";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Test]
    public void ToResponse_Staff_NeverCarriesFreeTextFromTheLog()
    {
        foreach (var entry in EntriesHoldingTheCanary())
            Serialised(entry, isAdmin: false).Should().NotContain(Canary, entry.Action.ToString());
    }

    [Test]
    public void ToResponse_Admin_CarriesTheFreeText()
    {
        // The entries whose details hold free text: the notes saved, the details edited, and each status change
        // made with a reason (the automatic ones have none), and every entry about a file (its name).
        var withText = EntriesHoldingTheCanary().Where(e => e.Details.Contains(Canary)).ToList();

        withText.Select(e => e.Action).Should().BeEquivalentTo(
            [
                Action.StageNotesUpdated, Action.StatusChanged, Action.StatusChanged, Action.StatusChanged, Action.DetailsUpdated,
                Action.AttachmentAdded, Action.AttachmentAdded, Action.AttachmentAdded, Action.AttachmentDeleted,
                Action.AttachmentDownloaded,
            ]);
        foreach (var entry in withText)
            Serialised(entry, isAdmin: true).Should().Contain(Canary, entry.Action.ToString());
    }

    [Test]
    public void EntriesHoldingTheCanary_CoverEveryAction()
    {
        EntriesHoldingTheCanary().Select(e => e.Action).Distinct().Should().BeEquivalentTo(Enum.GetValues<Action>());
    }

    /// <summary>As the API sends it, for a procedure that still exists (staff never get any other).</summary>
    private static string Serialised(PatientProcedureAuditEntry entry, bool isAdmin) =>
        JsonSerializer.Serialize(TimelineMapper.ToResponse(new PatientTimelineRow(entry, CurrentTitle), isAdmin), Json);

    /// <summary>Every action, recorded through <see cref="ProcedureChange"/> with the canary as all its free text.</summary>
    private static List<PatientProcedureAuditEntry> EntriesHoldingTheCanary()
    {
        var procedure = CreateProcedure(Status.Assigned, title: Canary);
        procedure.Notes = Canary;
        procedure.StageAt(1).Notes = Canary;
        var change = new ProcedureChange(procedure, Staff, Now);

        // As CreatePatientProcedureHandler, UpdatePatientProcedureHandler and DeletePatientProcedureHandler audit.
        change.Audit(Action.Created, new { template = TemplateName, dueDate = Today, priority = "High", stageCount = 3 });
        change.UpdateStageNotes(procedure.StageAt(1), $"{Canary} (edited)");
        change.FinishStage(procedure.StageAt(1));
        change.UnlockStage(procedure.StageAt(1));
        change.ChangeStatus(Status.Blocked, Canary);
        change.ChangeStatus(Status.InProgress, Canary);
        change.ChangeStatus(Status.Cancelled, Canary);
        change.Unassign(procedure.Assignments[0], Staff.Username);
        change.Assign(OtherUserId, OtherUsername);
        change.Audit(Action.DetailsUpdated, new
        {
            changes = new Dictionary<string, object>
            {
                ["title"] = new { from = Canary, to = $"{Canary} (edited)" },
                ["notes"] = new { from = Canary, to = $"{Canary} (edited)" },
                ["dueDate"] = new { from = Today, to = Today.AddDays(1) },
                ["priority"] = new { from = "Medium", to = "High" },
            },
        });
        change.Audit(Action.Deleted, new { status = procedure.Status.ToString() });

        // As the attachment handlers audit: a file on the patient, the procedure and a stage, deleted and downloaded.
        var patientFile = new Attachment { Id = Guid.NewGuid(), PatientId = procedure.PatientId, FileName = Canary, ContentType = "application/pdf" };
        var procedureFile = new Attachment { Id = Guid.NewGuid(), PatientId = procedure.PatientId, ProcedureId = procedure.Id, FileName = Canary, ContentType = "application/pdf" };
        var stageFile = new Attachment { Id = Guid.NewGuid(), PatientId = procedure.PatientId, ProcedureId = procedure.Id, StageId = procedure.StageAt(1).Id, FileName = Canary, ContentType = "image/png" };
        var onPatient = new AttachmentTarget(procedure.PatientId, null, null);
        var onProcedure = new AttachmentTarget(procedure.PatientId, procedure, null);
        var onStage = new AttachmentTarget(procedure.PatientId, procedure, procedure.StageAt(1));
        var files = new[]
        {
            AttachmentAudit.Entry(patientFile, onPatient, Staff, Action.AttachmentAdded, Now),
            AttachmentAudit.Entry(procedureFile, onProcedure, Staff, Action.AttachmentAdded, Now),
            AttachmentAudit.Entry(stageFile, onStage, Staff, Action.AttachmentAdded, Now),
            AttachmentAudit.Entry(stageFile, onStage, Staff, Action.AttachmentDeleted, Now),
            AttachmentAudit.Entry(procedureFile, onProcedure, Staff, Action.AttachmentDownloaded, Now),
        };

        return change.AuditEntries.Concat(files).ToList();
    }
}
