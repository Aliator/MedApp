using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.Common;

/// <summary>Procedure notes: the change that adds and edits them, the rule of when, and what the audit names.</summary>
[TestFixture]
public sealed class ProcedureNotesTests
{
    [Test]
    public void AddNote_ThenEdits_WriteAVersionEachAndAuditEachInOrder()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var change = new ProcedureChange(procedure, Staff, Now);

        var note = change.AddNote("First.");
        change.EditNote(note, "Second.");
        change.EditNote(note, "Third.");

        note.Version.Should().Be(3);
        note.Text.Should().Be("Third.");
        change.NoteVersions.Select(v => (v.Version, v.Text)).Should().Equal((1, "First."), (2, "Second."), (3, "Third."));
        change.AuditEntries.Select(e => e.Action).Should().Equal(Action.NoteAdded, Action.NoteEdited, Action.NoteEdited);
        change.AuditEntries.Select(e => e.AtUtc).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        change.AuditEntries.Select(ProcedureNotes.VersionOf).Should().Equal(
            new NoteVersionKey(note.Id, 1), new NoteVersionKey(note.Id, 2), new NoteVersionKey(note.Id, 3));
        change.HasChanges.Should().BeTrue();
    }

    [Test]
    public void EditNote_SameText_ChangesNothing()
    {
        var procedure = CreateProcedure(Status.InProgress);
        var note = procedure.AddNote();
        var change = new ProcedureChange(procedure, Staff, Now);

        change.EditNote(note, NoteText);

        change.HasChanges.Should().BeFalse();
        note.Version.Should().Be(1);
        procedure.LastUpdated.Should().NotBe(Now);
    }

    [TestCase(Status.Unassigned, true)]
    [TestCase(Status.Assigned, true)]
    [TestCase(Status.InProgress, true)]
    [TestCase(Status.InReview, true)]
    [TestCase(Status.Blocked, true)]
    [TestCase(Status.Completed, true)]
    [TestCase(Status.Cancelled, false)]
    [TestCase(Status.Archived, false)]
    public void CanChangeNotes_FollowsTheStatus(Status status, bool expected)
    {
        ProcedureWorkflow.CanChangeNotes(CreateProcedure(status)).Should().Be(expected);
    }

    [Test]
    public void VersionOf_OtherActionsAndBadDetails_AreNull()
    {
        var procedure = CreateProcedure();
        var created = ProcedureAudit.Entry(procedure, Staff, Action.Created, Now);
        var broken = ProcedureAudit.Entry(procedure, Staff, Action.NoteAdded, Now);
        broken.Details = "not json";
        var missing = ProcedureAudit.Entry(procedure, Staff, Action.NoteEdited, Now, details: new { noteId = Guid.NewGuid() });

        ProcedureNotes.VersionOf(created).Should().BeNull();
        ProcedureNotes.VersionOf(broken).Should().BeNull();
        ProcedureNotes.VersionOf(missing).Should().BeNull();
    }

    [Test]
    public void NoteEntries_AreOfTheNotesKind()
    {
        var procedure = CreateProcedure();
        var change = new ProcedureChange(procedure, Staff, Now);
        change.EditNote(change.AddNote("First."), "Second.");

        change.AuditEntries.Select(PatientTimelineKinds.Of).Should().AllBeEquivalentTo(PatientTimelineKind.Notes);
        change.AuditEntries.AsQueryable().WhereKindIn(new HashSet<PatientTimelineKind> { PatientTimelineKind.Notes })
            .Should().HaveCount(2);
        change.AuditEntries.AsQueryable().WhereKindIn(new HashSet<PatientTimelineKind> { PatientTimelineKind.Details })
            .Should().BeEmpty();
    }

    [Test]
    public void NoteChanges_CountAsChangesForRetention()
    {
        // Adding or editing a note is a change to the record: it moves the conclusion of treatment.
        RetentionSchedule.ReadOnlyAuditActions.Should().NotContain([Action.NoteAdded, Action.NoteEdited]);
    }

    [Test]
    public void Record_CreationWithNotes_MakesThemTheFirstNoteByTheCreator()
    {
        var procedure = CreateProcedure(Status.Unassigned);
        var change = new ProcedureChange(procedure, Staff, Now);

        ProcedureCreation.Record(change, TemplateName, Today.AddDays(7), NoteText);

        var note = procedure.Notes.Should().ContainSingle().Subject;
        note.AuthorUserId.Should().Be(Staff.UserId);
        change.AuditEntries.Select(e => e.Action).Should().StartWith([Action.Created, Action.NoteAdded]);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  \n ")]
    public void Record_CreationWithoutNotes_AddsNone(string? notes)
    {
        var procedure = CreateProcedure(Status.Unassigned);
        var change = new ProcedureChange(procedure, Staff, Now);

        ProcedureCreation.Record(change, TemplateName, Today.AddDays(7), notes);

        procedure.Notes.Should().BeEmpty();
        change.AuditEntries.Should().NotContain(e => e.Action == Action.NoteAdded);
    }
}
