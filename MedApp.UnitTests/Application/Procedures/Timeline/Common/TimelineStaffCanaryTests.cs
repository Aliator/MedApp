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
using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;

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
        // The entries whose details hold free text: the notes saved, the stage finished without its visit (the
        // admin's reason), the details edited, each status change made with a reason (the automatic ones have none),
        // and every entry about a file (its name).
        var withText = EntriesHoldingTheCanary().Where(e => e.Details.Contains(Canary)).ToList();

        withText.Select(e => e.Action).Should().BeEquivalentTo(
            [
                Action.StageNotesUpdated, Action.StageFinished, Action.StatusChanged, Action.StatusChanged, Action.StatusChanged,
                Action.DetailsUpdated, Action.AttachmentAdded, Action.AttachmentAdded, Action.AttachmentAdded,
                Action.AttachmentDeleted, Action.AttachmentDownloaded,
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
        // As FinishProcedureStageHandler records an admin finishing a Required stage without its visit.
        change.FinishStage(procedure.StageAt(1), new FinishedWithoutVisit(VisitRequirement.Required, Canary));
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

    [Test]
    public void ToResponse_StaffFinishedWithoutVisit_SaysSoButNotTheReason()
    {
        var finished = EntriesHoldingTheCanary().Single(e => e.Action == Action.StageFinished);

        var response = TimelineMapper.ToResponse(new PatientTimelineRow(finished, CurrentTitle), isAdmin: false);

        response.FinishedWithoutVisit.Should().BeTrue();
        response.VisitRequirement.Should().Be("Required");
        response.Reason.Should().BeNull();
    }

    [Test]
    public void ToResponse_StaffAppointmentEntries_NeverCarryFreeTextFromTheHistory()
    {
        foreach (var entry in AppointmentEventsHoldingTheCanary())
            Serialised(entry, isAdmin: false).Should().NotContain(Canary, entry.Action.ToString());
    }

    [Test]
    public void ToResponse_AdminAppointmentEntries_CarryTheFreeText()
    {
        // The entries whose details hold free text: the cancellation's reason and the notes saved (old and new).
        var withText = AppointmentEventsHoldingTheCanary().Where(e => e.Details.Contains(Canary)).ToList();

        withText.Select(e => e.Action).Should().BeEquivalentTo([AppointmentEventAction.NotesUpdated, AppointmentEventAction.Cancelled]);
        foreach (var entry in withText)
            Serialised(entry, isAdmin: true).Should().Contain(Canary, entry.Action.ToString());
    }

    [Test]
    public void AppointmentEventsHoldingTheCanary_CoverEveryAction()
    {
        AppointmentEventsHoldingTheCanary().Select(e => e.Action).Distinct()
            .Should().BeEquivalentTo(Enum.GetValues<AppointmentEventAction>());
    }

    /// <summary>An appointment history entry as the API sends it, for a procedure that still exists.</summary>
    private static string Serialised(AppointmentEvent entry, bool isAdmin) =>
        JsonSerializer.Serialize(TimelineMapper.ToResponse(new AppointmentTimelineRow(entry, true, CurrentTitle), isAdmin), Json);

    /// <summary>
    /// Every appointment history action, recorded through <see cref="ProcedureChange"/> and <see cref="AppointmentChange"/>
    /// as the handlers record them, with the canary as all the free text: the procedure's title, the notes (old and
    /// new) and the cancellation reason.
    /// </summary>
    private static List<AppointmentEvent> AppointmentEventsHoldingTheCanary()
    {
        var procedure = CreateProcedure(Status.Assigned, title: Canary);
        var procedureChange = new ProcedureChange(procedure, Staff, Now);
        var appointment = procedureChange.RequestVisit(procedure.StageAt(1));
        appointment.Notes = Canary;

        var change = new AppointmentChange(appointment, Staff, Now.AddMinutes(1), procedureTitle: Canary, stageName: ConsultationStage);
        change.Book(Staff.UserId, Staff.Username, Now.AddDays(1), Now.AddDays(1).AddMinutes(30));
        change.Book(OtherUserId, OtherUsername, Now.AddDays(2), Now.AddDays(2).AddMinutes(30));
        change.UpdateNotes($"{Canary} (edited)");
        change.RecordOutcome(AppointmentStatus.Attended);
        change.RecordOutcome(AppointmentStatus.DidNotAttend);

        var other = procedureChange.RequestVisit(procedure.StageAt(2));
        var cancel = new AppointmentChange(other, Staff, Now.AddMinutes(2), procedureTitle: Canary);
        cancel.Cancel(Canary);

        return [.. procedureChange.AppointmentEvents, .. change.Events, .. cancel.Events];
    }
}
