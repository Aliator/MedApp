using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Attachments;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Pathways.Timeline.Common;

/// <summary>
/// A canary for the timeline's rule that staff never get free text from the audit log: every action is recorded the
/// way handlers record it, with one unique string as every piece of free text (stage notes, pathway notes, titles,
/// reasons, a note's deletion reason and file names). The whole staff response, serialised as the API sends it, must never hold it, whatever fields are
/// added later; an admin's must. A pathway note's text isn't in the audit at all: the handler reads it from the note
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
        notes.Should().AllSatisfy(e => PathwayNotes.VersionOf(e).Should().NotBeNull());
    }

    [Test]
    public void ToResponse_Admin_CarriesTheFreeText()
    {
        // The entries whose details hold free text: the notes saved, the stage finished without its visit (the
        // admin's reason), the details edited, each status change made with a reason (the automatic ones have none),
        // every entry about a file (its name), and a note deleted (the admin's reason).
        // And the notes added, edited and deleted, whose text comes from the note versions.
        var withText = RowsHoldingTheCanary().Where(r => r.Entry.Details.Contains(Canary) || r.NoteText is not null).ToList();

        withText.Select(r => r.Entry.Action).Should().BeEquivalentTo(
            [
                Action.StageNotesUpdated, Action.StageFinished, Action.StatusChanged, Action.StatusChanged, Action.StatusChanged,
                Action.DetailsUpdated, Action.AttachmentAdded, Action.AttachmentAdded, Action.AttachmentAdded,
                Action.AttachmentAdded, Action.AttachmentDeleted, Action.AttachmentDownloaded, Action.NoteAdded, Action.NoteEdited, Action.NoteDeleted,
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
    public void ToResponse_NoteDeleted_StaffSeeThatItWasAndAdminsTheTextAndReason()
    {
        var deleted = RowsHoldingTheCanary().Single(r => r.Entry.Action == Action.NoteDeleted);

        var staff = TimelineMapper.ToResponse(deleted, isAdmin: false);
        var admin = TimelineMapper.ToResponse(deleted, isAdmin: true);

        staff.Kind.Should().Be(nameof(PatientTimelineKind.Notes));
        staff.Action.Should().Be(nameof(Action.NoteDeleted));
        staff.Reason.Should().BeNull();
        staff.Changes.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = TimelineMapper.NoteField, From = (string?)null, To = (string?)null });
        admin.Reason.Should().Be($"Other: {Canary}");
        admin.Changes.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = TimelineMapper.NoteField, From = $"{Canary} (edited)", To = (string?)null });
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

    /// <summary>As the API sends it, for a pathway that still exists (staff never get any other).</summary>
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
                Action.NoteDeleted => new PatientTimelineRow(e, CurrentTitle) { NoteText = $"{Canary} (edited)", PreviousNoteText = Canary },
                _ => new PatientTimelineRow(e, CurrentTitle),
            })
            .ToList();

    /// <summary>Every action, recorded through <see cref="PathwayChange"/> with the canary as all its free text.</summary>
    private static List<PatientPathwayAuditEntry> EntriesHoldingTheCanary()
    {
        var pathway = CreatePathway(Status.Assigned, title: Canary);
        pathway.StageAt(1).Notes = Canary;
        var change = new PathwayChange(pathway, Staff, Now);

        // As CreatePatientPathwayHandler, UpdatePatientPathwayHandler and DeletePatientPathwayHandler audit.
        change.Audit(Action.Created, new { template = TemplateName, dueDate = Today, priority = "High", stageCount = 3 });
        // As AddPathwayNoteHandler and EditPathwayNoteHandler: the canary as the note's text, which the audit never copies.
        var note = change.AddNote(Canary);
        change.EditNote(note, $"{Canary} (edited)");
        // As DeletePathwayNoteHandler and DecideNoteDeletionRequestHandler: the canary as the admin's reason.
        change.DeleteNote(note, NoteDeletionReason.Other, Canary, Guid.NewGuid());
        change.UpdateStageNotes(pathway.StageAt(1), $"{Canary} (edited)");
        // As FinishPathwayStageHandler records an admin finishing a Required stage without its visit.
        change.FinishStage(pathway.StageAt(1), new FinishedWithoutVisit(VisitRequirement.Required, Canary));
        change.UnlockStage(pathway.StageAt(1));
        change.ChangeStatus(Status.Blocked, Canary);
        change.ChangeStatus(Status.InProgress, Canary);
        change.ChangeStatus(Status.Cancelled, Canary);
        change.Unassign(pathway.Assignments[0], Staff.Username);
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
        change.Audit(Action.Deleted, new { status = pathway.Status.ToString() });

        // As the attachment handlers audit: a file on the patient, the pathway and a stage, deleted and downloaded.
        var patientFile = new Attachment { Id = Guid.NewGuid(), PatientId = pathway.PatientId, FileName = Canary, ContentType = "application/pdf" };
        var pathwayFile = new Attachment { Id = Guid.NewGuid(), PatientId = pathway.PatientId, PathwayId = pathway.Id, FileName = Canary, ContentType = "application/pdf" };
        var stageFile = new Attachment { Id = Guid.NewGuid(), PatientId = pathway.PatientId, PathwayId = pathway.Id, StageId = pathway.StageAt(1).Id, FileName = Canary, ContentType = "image/png" };
        var onPatient = new AttachmentTarget(pathway.PatientId, null, null);
        var onPathway = new AttachmentTarget(pathway.PatientId, pathway, null);
        var onStage = new AttachmentTarget(pathway.PatientId, pathway, pathway.StageAt(1));
        // A referral's letter.
        var referral = ReferralsTestConstants.CreateReferral();
        var letter = new Attachment { Id = Guid.NewGuid(), PatientId = pathway.PatientId, ReferralId = referral.Id, FileName = Canary, ContentType = "application/pdf" };
        var onReferral = new AttachmentTarget(pathway.PatientId, null, null) { Referral = referral };
        var files = new[]
        {
            AttachmentAudit.Entry(letter, onReferral, Staff, Action.AttachmentAdded, Now),
            AttachmentAudit.Entry(patientFile, onPatient, Staff, Action.AttachmentAdded, Now),
            AttachmentAudit.Entry(pathwayFile, onPathway, Staff, Action.AttachmentAdded, Now),
            AttachmentAudit.Entry(stageFile, onStage, Staff, Action.AttachmentAdded, Now),
            AttachmentAudit.Entry(stageFile, onStage, Staff, Action.AttachmentDeleted, Now),
            AttachmentAudit.Entry(pathwayFile, onPathway, Staff, Action.AttachmentDownloaded, Now),
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
        // The entries whose details hold free text: the cancellation's reason, the notes saved (old and new), and why a walk-in was withdrawn.
        var withText = AppointmentEventsHoldingTheCanary().Where(e => e.Details.Contains(Canary)).ToList();

        withText.Select(e => e.Action).Should().BeEquivalentTo([AppointmentEventAction.NotesUpdated, AppointmentEventAction.Cancelled, AppointmentEventAction.EnteredInError]);
        foreach (var entry in withText)
            Serialised(entry, isAdmin: true).Should().Contain(Canary, entry.Action.ToString());
    }

    [Test]
    public void AppointmentEventsHoldingTheCanary_CoverEveryAction()
    {
        AppointmentEventsHoldingTheCanary().Select(e => e.Action).Distinct()
            .Should().BeEquivalentTo(Enum.GetValues<AppointmentEventAction>());
    }

    [Test]
    public void ToResponse_StaffReferralEntries_NeverCarryFreeText()
    {
        foreach (var row in ReferralRowsHoldingTheCanary())
            Serialised(row, isAdmin: false).Should().NotContain(Canary, row.Event.Action.ToString());
    }

    [Test]
    public void ReferralEvents_NeverHoldFreeText()
    {
        // DPIA risk 27: the reason, the referrer and the details given live only on the referral.
        ReferralRowsHoldingTheCanary().Should().AllSatisfy(r => r.Event.Details.Should().NotContain(Canary));
    }

    [Test]
    public void ToResponse_AdminReferralEntries_CarryTheReferralsFreeText()
    {
        // Registered (its reason), Withdrawn and EnteredInError (the detail given), each with the referring practice.
        foreach (var row in ReferralRowsHoldingTheCanary())
            Serialised(row, isAdmin: true).Should().Contain(Canary, row.Event.Action.ToString());
    }

    [Test]
    public void ToResponse_ReferralEdit_GivesStructuredValuesAndOnlyTheNamesOfFreeTextFields()
    {
        var edited = ReferralRowsHoldingTheCanary().Single(r => r.Event.Action == ReferralEventAction.DetailsEdited);

        var response = TimelineMapper.ToResponse(edited, isAdmin: false);

        response.Kind.Should().Be(nameof(PatientTimelineKind.Referrals));
        response.Action.Should().Be("ReferralDetailsEdited");
        response.ReferralId.Should().Be(edited.Event.ReferralId);
        response.PathwayId.Should().BeNull();
        response.Changes.Single(c => c.Field == "requestedPriority").Should().BeEquivalentTo(new { From = "High", To = "Urgent" });
        response.Changes.Single(c => c.Field == "reason").Should().BeEquivalentTo(new { From = (string?)null, To = (string?)null });
    }

    [Test]
    public void ReferralRowsHoldingTheCanary_CoverEveryActionWrittenSoFar()
    {
        // Triage and the waiting-time clock add the rest; add them here when they're written.
        ReferralRowsHoldingTheCanary().Select(r => r.Event.Action).Should().BeEquivalentTo(
            [ReferralEventAction.Registered, ReferralEventAction.DetailsEdited, ReferralEventAction.Withdrawn, ReferralEventAction.EnteredInError]);
    }

    private static string Serialised(ReferralTimelineRow row, bool isAdmin) =>
        JsonSerializer.Serialize(TimelineMapper.ToResponse(row, isAdmin), Json);

    /// <summary>
    /// Every referral action written so far, through <see cref="ReferralChange"/> as the handlers write it, with the
    /// canary as all the referral's free text (its reason, referrer and the detail given), and each row carrying that
    /// text as the timeline's search gives it to admins.
    /// </summary>
    private static List<ReferralTimelineRow> ReferralRowsHoldingTheCanary()
    {
        var referral = ReferralsTestConstants.CreateReferral();
        referral.Reason = Canary;
        referral.ReferrerPractice = Canary;
        referral.ReferrerClinician = Canary;
        var change = new ReferralChange(referral, Staff, Now);
        change.Register(referral.Specialty);
        change.EditDetails(
            new ReferralDetails(referral.SpecialtyId, referral.ReceivedOn, $"{Canary} (edited)", null, null,
                PatientPathwayPriority.Urgent, $"{Canary} (edited)"),
            referral.Specialty, referral.Specialty);
        change.Withdraw(Canary);

        var other = ReferralsTestConstants.CreateReferral();
        var error = new ReferralChange(other, Staff, Now);
        error.MarkEnteredInError(Canary);

        return change.Events.Concat(error.Events)
            .Select(e => new ReferralTimelineRow(e, Canary, Canary, Canary))
            .ToList();
    }

    /// <summary>An appointment history entry as the API sends it, for a pathway that still exists.</summary>
    private static string Serialised(AppointmentEvent entry, bool isAdmin) =>
        JsonSerializer.Serialize(TimelineMapper.ToResponse(new AppointmentTimelineRow(entry, true, CurrentTitle), isAdmin), Json);

    /// <summary>
    /// Every appointment history action, recorded through <see cref="PathwayChange"/> and <see cref="AppointmentChange"/>
    /// as the handlers record them, with the canary as all the free text: the pathway's title, the notes (old and
    /// new) and the cancellation reason.
    /// </summary>
    private static List<AppointmentEvent> AppointmentEventsHoldingTheCanary()
    {
        var pathway = CreatePathway(Status.Assigned, title: Canary);
        var pathwayChange = new PathwayChange(pathway, Staff, Now);
        var appointment = pathwayChange.RequestVisit(pathway.StageAt(1));
        appointment.Notes = Canary;

        var change = new AppointmentChange(appointment, Staff, Now.AddMinutes(1), pathwayTitle: Canary, stageName: ConsultationStage);
        change.Book(Staff.UserId, Staff.Username, Now.AddDays(1), Now.AddDays(1).AddMinutes(30));
        change.Book(OtherUserId, OtherUsername, Now.AddDays(2), Now.AddDays(2).AddMinutes(30));
        change.UpdateNotes($"{Canary} (edited)");
        change.RecordOutcome(AppointmentStatus.Attended);
        change.RecordOutcome(AppointmentStatus.DidNotAttend);

        var other = pathwayChange.RequestVisit(pathway.StageAt(2));
        var cancel = new AppointmentChange(other, Staff, Now.AddMinutes(2), pathwayTitle: Canary);
        cancel.Cancel(Canary);

        // A walk-in recorded after the event, as the stage's visit in Requires Appointment, with notes.
        var walkIn = pathwayChange.RequestVisit(pathway.StageAt(3));
        walkIn.Notes = Canary;
        var recorded = new AppointmentChange(walkIn, Staff, Now.AddMinutes(3), pathwayTitle: Canary, stageName: ConsultationStage);
        recorded.RecordAfterEvent(OtherUserId, OtherUsername, Now.AddDays(-1), Now.AddDays(-1).AddMinutes(30), AppointmentStatus.Attended, wasRequested: true);
        recorded.MarkEnteredInError(Canary);

        return [.. pathwayChange.AppointmentEvents, .. change.Events, .. cancel.Events, .. recorded.Events];
    }
}
