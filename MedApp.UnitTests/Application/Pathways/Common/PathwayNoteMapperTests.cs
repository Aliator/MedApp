using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Common;

[TestFixture]
public sealed class PathwayNoteMapperTests
{
    [Test]
    public void ToResponse_Note_HasItsAuthorTimesAndVersion()
    {
        var note = CreatePathway().AddNote(Staff.UserId, version: 2);

        var response = PathwayNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: true);

        response.Should().BeEquivalentTo(new
        {
            note.Id,
            AuthorUserId = (Guid?)Staff.UserId,
            AuthorUsername = Staff.Username,
            Text = NoteText,
            note.CreatedAtUtc,
            note.LastEditedAtUtc,
            Version = 2,
            CanEdit = true
        });
    }

    [Test]
    public void ToResponse_AuthorsAccountGone_IsDeletedUser()
    {
        var note = CreatePathway().AddNote(Guid.NewGuid());

        PathwayNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: true).AuthorUsername
            .Should().Be(PathwayMapper.DeletedUser);
    }

    [Test]
    public void ToResponse_NoAuthorRecorded_IsUnknownAndCantBeEdited()
    {
        var note = new PatientPathwayNote { Id = Guid.NewGuid(), AuthorUserId = null, Text = NoteText, Version = 1 };

        var response = PathwayNoteMapper.ToResponse(note, Usernames, null, canChange: true);

        response.AuthorUsername.Should().Be(PathwayNoteMapper.UnknownAuthor);
        response.CanEdit.Should().BeFalse("a null viewer must not match a null author");
    }

    [Test]
    public void ToResponse_SomeoneElsesNote_CantBeEdited()
    {
        var note = CreatePathway().AddNote(OtherUserId);

        PathwayNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: true).CanEdit.Should().BeFalse();
        PathwayNoteMapper.ToResponse(note, Usernames, Admin.UserId, canChange: true).CanEdit.Should().BeFalse();
    }

    [Test]
    public void ToResponse_OwnNoteWhileNotesCantChange_CantBeEdited()
    {
        var note = CreatePathway().AddNote(Staff.UserId);

        PathwayNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: false).CanEdit.Should().BeFalse();
    }

    [Test]
    public void ToResponses_AreNewestFirst()
    {
        var pathway = CreatePathway();
        var oldest = pathway.AddNote(createdAt: Now.AddDays(-3));
        var newest = pathway.AddNote(createdAt: Now.AddHours(-1));
        var middle = pathway.AddNote(createdAt: Now.AddDays(-1));

        PathwayNoteMapper.ToResponses(pathway.Notes, Usernames, Staff.UserId, canChange: true).Select(n => n.Id)
            .Should().Equal(newest.Id, middle.Id, oldest.Id);
    }

    [Test]
    public void FirstNoteText_IsTheOldestNotesText()
    {
        var pathway = CreatePathway();
        pathway.LegacyNotes = "Synthetic old field.";
        pathway.AddNote(text: "Synthetic newer note.", createdAt: Now.AddHours(-1));
        pathway.AddNote(text: "Synthetic first note.", createdAt: Now.AddDays(-3));

        PathwayNoteMapper.FirstNoteText(pathway).Should().Be("Synthetic first note.");
    }

    [Test]
    public void FirstNoteText_NoNotes_IsTheOldField()
    {
        var pathway = CreatePathway();
        pathway.LegacyNotes = "Synthetic text an older copy of the API wrote.";

        PathwayNoteMapper.FirstNoteText(pathway).Should().Be(pathway.LegacyNotes);
    }

    [Test]
    public void PathwayMapper_ToResponse_KeepsTheOldNotesStringForOldPages()
    {
        var pathway = CreatePathway();
        pathway.AddNote(text: "Synthetic first note.", createdAt: Now.AddDays(-3));
        pathway.AddNote(text: "Synthetic newer note.", createdAt: Now.AddHours(-1));

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now, viewerId: Staff.UserId);

        response.Notes.Should().Be("Synthetic first note.");
        response.NoteList.Should().HaveCount(2);
    }

    [Test]
    public void PathwayMapper_ToResponse_CarriesTheNotesForTheViewer()
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 3);
        var own = pathway.AddNote(Staff.UserId);
        pathway.AddNote(OtherUserId);

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now, viewerId: Staff.UserId);

        response.CanAddNotes.Should().BeTrue("notes can be added after completion");
        response.NoteList.Should().HaveCount(2);
        response.NoteList.Single(n => n.CanEdit).Id.Should().Be(own.Id);
        PathwayMapper.UserIds(pathway).Should().Contain(OtherUserId);
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public void PathwayMapper_ToResponse_ClosedPathway_CantHaveNotesAddedOrEdited(Status status)
    {
        var pathway = CreatePathway(status);
        pathway.AddNote(Staff.UserId);

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now, viewerId: Staff.UserId);

        response.CanAddNotes.Should().BeFalse();
        response.NoteList.Should().ContainSingle().Which.CanEdit.Should().BeFalse();
    }

    [Test]
    public void ToResponse_ForStaff_CanBeAskedForButNotDeleted()
    {
        // The flags say what the signed-in user can do: staff ask, admins delete.
        var note = CreatePathway().AddNote(OtherUserId);

        var response = PathwayNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: true);

        response.Should().BeEquivalentTo(new { CanDelete = false, CanRequestDeletion = true, DeletionRequestId = (Guid?)null });
    }

    [Test]
    public void ToResponse_ForAnAdmin_CanBeDeletedButNotAskedFor()
    {
        var note = CreatePathway().AddNote(OtherUserId);

        var response = PathwayNoteMapper.ToResponse(note, Usernames, Admin.UserId, canChange: true, viewerIsAdmin: true);

        response.Should().BeEquivalentTo(new { CanDelete = true, CanRequestDeletion = false });
    }

    [Test]
    public void ToResponse_RequestWaiting_ShowsItAndCantBeAskedForAgain()
    {
        var pathway = CreatePathway();
        var note = pathway.AddNote();
        var request = pathway.RequestDeletion(note);

        PathwayNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: true)
            .Should().BeEquivalentTo(new { CanDelete = false, CanRequestDeletion = false, DeletionRequestId = (Guid?)request.Id });
        PathwayNoteMapper.ToResponse(note, Usernames, Admin.UserId, canChange: true, viewerIsAdmin: true)
            .Should().BeEquivalentTo(new { CanDelete = true, DeletionRequestId = (Guid?)request.Id });
    }

    [Test]
    public void ToResponse_ClosedPathwayButRecordCanChange_CanStillBeDeletedOrAskedFor()
    {
        // Deleting is a correction: allowed in any status, while the record itself can change.
        var note = CreatePathway(Status.Cancelled).AddNote(Staff.UserId);

        PathwayNoteMapper.ToResponse(note, Usernames, Staff.UserId, canChange: false, recordCanChange: true)
            .Should().BeEquivalentTo(new { CanEdit = false, CanDelete = false, CanRequestDeletion = true });
        PathwayNoteMapper.ToResponse(note, Usernames, Admin.UserId, canChange: false, recordCanChange: true, viewerIsAdmin: true)
            .Should().BeEquivalentTo(new { CanEdit = false, CanDelete = true, CanRequestDeletion = false });
    }

    [Test]
    public void PathwayMapper_ToResponse_PassesWhetherTheViewerIsAnAdmin()
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.AddNote(OtherUserId);

        PathwayMapper.ToResponse(pathway, Usernames, Now, viewerId: Admin.UserId, viewerIsAdmin: true).NoteList.Single().CanDelete.Should().BeTrue();
        PathwayMapper.ToResponse(pathway, Usernames, Now, viewerId: Staff.UserId).NoteList.Single().CanDelete.Should().BeFalse();
    }

    [Test]
    public void ToResponses_DeletedNotes_AreLeftOut()
    {
        var pathway = CreatePathway();
        var kept = pathway.AddNote();
        var deleted = pathway.AddNote(text: "Synthetic note on the wrong record.");
        new PathwayChange(pathway, Admin, Now).DeleteNote(deleted, NoteDeletionReason.WrongPatient, DeletionDetail);

        PathwayNoteMapper.ToResponses(pathway.Notes, Usernames, Staff.UserId, canChange: true)
            .Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Test]
    public void FirstNoteText_FirstNoteDeleted_IsTheNextOrEmptyButNeverTheOldField()
    {
        var pathway = CreatePathway();
        pathway.LegacyNotes = NoteText;
        var first = pathway.AddNote(createdAt: Now.AddDays(-3));
        var second = pathway.AddNote(text: "Synthetic second note.", createdAt: Now.AddDays(-2));
        var change = new PathwayChange(pathway, Admin, Now);

        change.DeleteNote(first, NoteDeletionReason.WrongPatient, DeletionDetail);
        PathwayNoteMapper.FirstNoteText(pathway).Should().Be("Synthetic second note.");

        change.DeleteNote(second, NoteDeletionReason.WrongPatient, DeletionDetail);
        PathwayNoteMapper.FirstNoteText(pathway).Should().BeEmpty("the old field's text was copied into the deleted note");
    }

    [Test]
    public void PathwayMapper_ToResponse_DeletedNote_IsHiddenAndItsDeleterLookedUp()
    {
        var pathway = CreatePathway(Status.InProgress);
        var deleted = pathway.AddNote();
        new PathwayChange(pathway, Admin, Now).DeleteNote(deleted, NoteDeletionReason.WrongPatient, DeletionDetail);

        PathwayMapper.UserIds(pathway).Should().Contain(Admin.UserId);
        PathwayMapper.ToResponse(pathway, Usernames, Now, viewerId: Staff.UserId).NoteList.Should().BeEmpty();
    }

    [Test]
    public void PathwayMapper_ToResponse_RestrictedPatient_CantHaveNotesAddedOrEdited()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now;
        var pathway = CreatePathway(Status.InProgress, patient: patient);
        pathway.AddNote(Staff.UserId);

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now, viewerId: Staff.UserId);

        response.ReadOnlyReason.Should().Be(PatientRules.WhyCantChange(patient));
        response.CanAddNotes.Should().BeFalse();
        response.NoteList.Single().Should().BeEquivalentTo(new { CanEdit = false, CanDelete = false, CanRequestDeletion = false });
    }
}
