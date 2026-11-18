using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Action = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Common;

/// <summary>Pathway notes: the change that adds and edits them, the rule of when, and what the audit names.</summary>
[TestFixture]
public sealed class PathwayNotesTests
{
    [Test]
    public void AddNote_ThenEdits_WriteAVersionEachAndAuditEachInOrder()
    {
        var pathway = CreatePathway(Status.InProgress);
        var change = new PathwayChange(pathway, Staff, Now);

        var note = change.AddNote("First.");
        change.EditNote(note, "Second.");
        change.EditNote(note, "Third.");

        note.Version.Should().Be(3);
        note.Text.Should().Be("Third.");
        change.NoteVersions.Select(v => (v.Version, v.Text)).Should().Equal((1, "First."), (2, "Second."), (3, "Third."));
        change.AuditEntries.Select(e => e.Action).Should().Equal(Action.NoteAdded, Action.NoteEdited, Action.NoteEdited);
        change.AuditEntries.Select(e => e.AtUtc).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        change.AuditEntries.Select(PathwayNotes.VersionOf).Should().Equal(
            new NoteVersionKey(note.Id, 1), new NoteVersionKey(note.Id, 2), new NoteVersionKey(note.Id, 3));
        change.HasChanges.Should().BeTrue();
    }

    [Test]
    public void EditNote_SameText_ChangesNothing()
    {
        var pathway = CreatePathway(Status.InProgress);
        var note = pathway.AddNote();
        var change = new PathwayChange(pathway, Staff, Now);

        change.EditNote(note, NoteText);

        change.HasChanges.Should().BeFalse();
        note.Version.Should().Be(1);
        pathway.LastUpdated.Should().NotBe(Now);
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
        PathwayWorkflow.CanChangeNotes(CreatePathway(status)).Should().Be(expected);
    }

    [Test]
    public void VersionOf_OtherActionsAndBadDetails_AreNull()
    {
        var pathway = CreatePathway();
        var created = PathwayAudit.Entry(pathway, Staff, Action.Created, Now);
        var broken = PathwayAudit.Entry(pathway, Staff, Action.NoteAdded, Now);
        broken.Details = "not json";
        var missing = PathwayAudit.Entry(pathway, Staff, Action.NoteEdited, Now, details: new { noteId = Guid.NewGuid() });

        PathwayNotes.VersionOf(created).Should().BeNull();
        PathwayNotes.VersionOf(broken).Should().BeNull();
        PathwayNotes.VersionOf(missing).Should().BeNull();
    }

    [Test]
    public void NoteEntries_AreOfTheNotesKind()
    {
        var pathway = CreatePathway();
        var change = new PathwayChange(pathway, Staff, Now);
        var note = change.AddNote("First.");
        change.EditNote(note, "Second.");
        change.DeleteNote(note, NoteDeletionReason.WrongPatient, DeletionDetail);

        change.AuditEntries.Select(PatientTimelineKinds.Of).Should().AllBeEquivalentTo(PatientTimelineKind.Notes);
        change.AuditEntries.AsQueryable().WhereKindIn(new HashSet<PatientTimelineKind> { PatientTimelineKind.Notes })
            .Should().HaveCount(3);
        change.AuditEntries.AsQueryable().WhereKindIn(new HashSet<PatientTimelineKind> { PatientTimelineKind.Details })
            .Should().BeEmpty();
    }

    [Test]
    public void NoteChanges_CountAsChangesForRetention()
    {
        // Adding or editing a note is a change to the record: it moves the conclusion of treatment.
        RetentionSchedule.ReadOnlyAuditActions.Should().NotContain([Action.NoteAdded, Action.NoteEdited, Action.NoteDeleted]);
    }

    [Test]
    public void DeleteNote_MarksItDeletedKeepsItAndAuditsTheReasonButNeverTheText()
    {
        var pathway = CreatePathway(Status.InProgress);
        var note = pathway.AddNote(version: 2);
        var requestId = Guid.NewGuid();
        note.PendingDeletionRequestId = requestId;
        var change = new PathwayChange(pathway, Admin, Now);

        change.DeleteNote(note, NoteDeletionReason.WrongPatient, DeletionDetail, requestId);

        note.Should().BeEquivalentTo(new
        {
            IsDeleted = true,
            DeletedAtUtc = (DateTime?)Now,
            DeletedByUserId = (Guid?)Admin.UserId,
            DeletionReason = (NoteDeletionReason?)NoteDeletionReason.WrongPatient,
            DeletionDetail,
            PendingDeletionRequestId = (Guid?)null,
            Text = NoteText,
            Version = 2
        });
        pathway.Notes.Should().Contain(note);
        pathway.LastUpdated.Should().Be(Now);
        change.DeletedNotes.Should().Equal(note);
        change.NoteVersions.Should().BeEmpty("deleting writes no new version");
        change.HasChanges.Should().BeTrue();
        var entry = change.AuditEntries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(Action.NoteDeleted);
        entry.UserId.Should().Be(Admin.UserId);
        entry.Details.Should().Be($$"""{"noteId":"{{note.Id}}","version":2,"deletionReason":"WrongPatient","detail":"{{DeletionDetail}}","requestId":"{{requestId}}"}""")
            .And.NotContain(NoteText);
        PathwayNotes.VersionOf(entry).Should().Be(new NoteVersionKey(note.Id, 2));
    }

    [Test]
    public void DeleteNote_TheOldestNote_ClearsTheOldNotesFieldItWasCopiedFrom()
    {
        // #110's migration copied the old single field into the pathway's first note and kept the column: deleting
        // that note must not leave its text there, to come back when the column is dropped.
        var pathway = CreatePathway(Status.InProgress);
        pathway.LegacyNotes = NoteText;
        var first = pathway.AddNote(createdAt: Now.AddDays(-5));
        pathway.AddNote(text: "Synthetic later note.", createdAt: Now.AddDays(-1));
        var change = new PathwayChange(pathway, Admin, Now);

        change.DeleteNote(first, NoteDeletionReason.WrongPatient, null);

        pathway.LegacyNotes.Should().BeEmpty();
        change.AuditEntries.Single().Details.Should().Contain("\"oldNotesFieldCleared\":true");
        PathwayNoteMapper.FirstNoteText(pathway).Should().Be("Synthetic later note.");
    }

    [Test]
    public void DeleteNote_ALaterNote_LeavesTheOldNotesField()
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.LegacyNotes = NoteText;
        pathway.AddNote(createdAt: Now.AddDays(-5));
        var later = pathway.AddNote(text: "Synthetic later note.", createdAt: Now.AddDays(-1));
        var change = new PathwayChange(pathway, Admin, Now);

        change.DeleteNote(later, NoteDeletionReason.Duplicate, null);

        pathway.LegacyNotes.Should().Be(NoteText);
        change.AuditEntries.Single().Details.Should().NotContain("oldNotesFieldCleared");
    }

    [Test]
    public void AlreadyRequestedMessage_NamesWhoAndWhen()
    {
        var pathway = CreatePathway();
        var request = pathway.RequestDeletion(pathway.AddNote(), new PathwayActor(OtherUserId, OtherUsername));

        PathwayNotes.AlreadyRequestedMessage(request).Should()
            .Be($"{OtherUsername} already asked for this note to be deleted, Sunday 14 June at 13:00. An admin will decide.");
        PathwayNotes.AlreadyRequestedMessage(null).Should().Be("Someone has already asked for this note to be deleted. An admin will decide.");
    }

    [Test]
    public void AlreadyDecidedMessage_WithoutWhoOrWhen_StillSaysWhatHappened()
    {
        var pathway = CreatePathway();
        var request = pathway.RequestDeletion(pathway.AddNote());
        request.Status = NoteDeletionRequestStatus.Approved;

        PathwayNotes.AlreadyDecidedMessage(request).Should().Be("Someone already deleted this note.");
    }

    [Test]
    public void Record_CreationWithNotes_MakesThemTheFirstNoteByTheCreator()
    {
        var pathway = CreatePathway(Status.Unassigned);
        var change = new PathwayChange(pathway, Staff, Now);

        PathwayCreation.Record(change, TemplateName, Today.AddDays(7), NoteText);

        var note = pathway.Notes.Should().ContainSingle().Subject;
        note.AuthorUserId.Should().Be(Staff.UserId);
        change.AuditEntries.Select(e => e.Action).Should().StartWith([Action.Created, Action.NoteAdded]);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  \n ")]
    public void Record_CreationWithoutNotes_AddsNone(string? notes)
    {
        var pathway = CreatePathway(Status.Unassigned);
        var change = new PathwayChange(pathway, Staff, Now);

        PathwayCreation.Record(change, TemplateName, Today.AddDays(7), notes);

        pathway.Notes.Should().BeEmpty();
        change.AuditEntries.Should().NotContain(e => e.Action == Action.NoteAdded);
    }
}
