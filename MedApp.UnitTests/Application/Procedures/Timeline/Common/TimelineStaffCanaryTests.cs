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
/// added later; an admin's must. A procedure note's text isn't in the audit at all: the handler reads it from the note
/// versions for admins (<see cref="PatientTimelineRow.NoteText"/>), so its rows carry the canary there.
/// </summary>
[TestFixture]
public sealed class TimelineStaffCanaryTests
{
    private const string Canary = "canary-text-7f3a";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Test]
    public void ToResponse_Staff_NeverCarriesFreeTextFromTheLog()
    {
        foreach (var row in RowsHoldingTheCanary())
            Serialised(row, isAdmin: false).Should().NotContain(Canary, row.Entry.Action.ToString());
    }

    [Test]
    public void NoteEntries_NeverHoldTheNotesText()
    {
        // DPIA risk 4: the audit log holds a note's ID and version, never its text.
        var notes = EntriesHoldingTheCanary().Where(e => e.Action is Action.NoteAdded or Action.NoteEdited).ToList();

        notes.Select(e => e.Action).Should().Equal(Action.NoteAdded, Action.NoteEdited);
        notes.Should().AllSatisfy(e => e.Details.Should().NotContain(Canary));
        notes.Should().AllSatisfy(e => ProcedureNotes.VersionOf(e).Should().NotBeNull());
    }

    [Test]
    public void ToResponse_Admin_CarriesTheFreeText()
    {
        // The entries whose details hold free text: the notes saved, the stage finished without its visit (the
        // admin's reason), the details edited, each status change made with a reason (the automatic ones have none),
        // and every entry about a file (its name).
        // And the notes added and edited, whose text comes from the note versions.
        var withText = RowsHoldingTheCanary().Where(r => r.Entry.Details.Contains(Canary) || r.NoteText is not null).ToList();

        withText.Select(r => r.Entry.Action).Should().BeEquivalentTo(
            [
                Action.StageNotesUpdated, Action.StageFinished, Action.StatusChanged, Action.StatusChanged, Action.StatusChanged,
                Action.DetailsUpdated, Action.AttachmentAdded, Action.AttachmentAdded, Action.AttachmentAdded,
                Action.AttachmentDeleted, Action.AttachmentDownloaded, Action.NoteAdded, Action.NoteEdited,
            ]);
        foreach (var row in withText)
            Serialised(row, isAdmin: true).Should().Contain(Canary, row.Entry.Action.ToString());
    }

    [Test]
    public void ToResponse_AdminNoteEdited_HasTheTextBeforeAndAfter()
    {
        var edited = RowsHoldingTheCanary().Single(r => r.Entry.Action == Action.NoteEdited);

        var response = TimelineMapper.ToResponse(edited, isAdmin: true);

        response.Kind.Should().Be(nameof(PatientTimelineKind.Notes));
        response.Changes.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = TimelineMapper.NoteField, From = Canary, To = $"{Canary} (edited)" });
    }

    [Test]
    public void ToResponse_StaffNoteAdded_SaysWhoAndWhenButNotTheText()
    {
        var added = RowsHoldingTheCanary().Single(r => r.Entry.Action == Action.NoteAdded);

        var response = TimelineMapper.ToResponse(added, isAdmin: false);

        response.Kind.Should().Be(nameof(PatientTimelineKind.Notes));
        response.Action.Should().Be(nameof(Action.NoteAdded));
        response.Username.Should().Be(Staff.Username);
        response.AtUtc.Should().Be(added.Entry.AtUtc);
        response.Changes.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = TimelineMapper.NoteField, From = (string?)null, To = (string?)null });
    }

    [Test]
    public void EntriesHoldingTheCanary_CoverEveryAction()
    {
        EntriesHoldingTheCanary().Select(e => e.Action).Distinct().Should().BeEquivalentTo(Enum.GetValues<Action>());
    }

    /// <summary>As the API sends it, for a procedure that still exists (staff never get any other).</summary>
    private static string Serialised(PatientTimelineRow row, bool isAdmin) =>
        JsonSerializer.Serialize(TimelineMapper.ToResponse(row, isAdmin), Json);

    /// <summary>
    /// Every entry as the timeline's handler gives it to the mapper: a note's text (and, for an edit, the text before)
    /// from the note versions, the canary each time, as it does for admins.
    /// </summary>
    private static List<PatientTimelineRow> RowsHoldingTheCanary() =>
        EntriesHoldingTheCanary()
            .Select(e => e.Action switch
            {
                Action.NoteAdded => new PatientTimelineRow(e, CurrentTitle) { NoteText = Canary },
                Action.NoteEdited => new PatientTimelineRow(e, CurrentTitle) { NoteText = $"{Canary} (edited)", PreviousNoteText = Canary },
                _ => new PatientTimelineRow(e, CurrentTitle),
            })
            .ToList();

    /// <summary>Every action, recorded through <see cref="ProcedureChange"/> with the canary as all its free text.</summary>
    private static List<PatientProcedureAuditEntry> EntriesHoldingTheCanary()
    {
        var procedure = CreateProcedure(Status.Assigned, title: Canary);
        procedure.StageAt(1).Notes = Canary;
        var change = new ProcedureChange(procedure, Staff, Now);

        // As CreatePatientProcedureHandler, UpdatePatientProcedureHandler and DeletePatientProcedureHandler audit.
        change.Audit(Action.Created, new { template = TemplateName, dueDate = Today, priority = "High", stageCount = 3 });
        // As AddProcedureNoteHandler and EditProcedureNoteHandler: the canary as the note's text, which the audit never copies.
        var note = change.AddNote(Canary);
        change.EditNote(note, $"{Canary} (edited)");
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

        // A walk-in recorded after the event, as the stage's visit in Requires Appointment, with notes.
        var walkIn = procedureChange.RequestVisit(procedure.StageAt(3));
        walkIn.Notes = Canary;
        var recorded = new AppointmentChange(walkIn, Staff, Now.AddMinutes(3), procedureTitle: Canary, stageName: ConsultationStage);
        recorded.RecordAfterEvent(OtherUserId, OtherUsername, Now.AddDays(-1), Now.AddDays(-1).AddMinutes(30), AppointmentStatus.Attended, wasRequested: true);

        return [.. procedureChange.AppointmentEvents, .. change.Events, .. cancel.Events, .. recorded.Events];
    }
}
