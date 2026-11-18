using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.EditPathwayNote;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.EditPathwayNote;

[TestFixture]
public sealed class EditPathwayNoteHandlerTests : PathwayHandlerTestBase
{
    private const string Edited = "Synthetic note: prefers an afternoon appointment.";

    private EditPathwayNoteHandler Handler => Fixture.Create<EditPathwayNoteHandler>();

    [Test]
    public async Task Handle_OwnNote_SavesTheNextVersionAndAuditsItsIdOnly()
    {
        var pathway = GivenPathway(CreatePathway(Status.Completed, finishedStages: 3));
        var note = pathway.AddNote(Staff.UserId);

        var response = await Handler.Handle(new EditPathwayNoteCommand(pathway.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        note.Should().BeEquivalentTo(new { Text = Edited, Version = 2, LastEditedAtUtc = (DateTime?)Now });
        pathway.LastUpdated.Should().Be(Now);
        var change = SavedChanges.Should().ContainSingle().Subject;
        change.NewNotes.Should().BeEmpty();
        change.NoteVersions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            NoteId = note.Id, Version = 2, Text = Edited, UserId = (Guid?)Staff.UserId, Staff.Username, AtUtc = Now
        });
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.NoteEdited);
        entry.Details.Should().Be($$"""{"noteId":"{{note.Id}}","version":2}""").And.NotContain(Edited).And.NotContain(NoteText);
        response.Should().BeEquivalentTo(new { note.Id, Text = Edited, Version = 2, LastEditedAtUtc = (DateTime?)Now, CanEdit = true });
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_TextUnchanged_SavesNothing()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(Staff.UserId);

        var response = await Handler.Handle(new EditPathwayNoteCommand(pathway.Id, note.Id, NoteText, 1, Staff), CancellationToken.None);

        response.Version.Should().Be(1);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_SomeoneElsesNote_ThrowsForbidden()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(OtherUserId);

        var act = () => Handler.Handle(new EditPathwayNoteCommand(pathway.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(PathwayNotes.OnlyTheAuthorMessage);
        note.Text.Should().Be(NoteText);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_AdminEditingSomeoneElsesNote_ThrowsForbidden()
    {
        // Clinical record integrity: what someone wrote stays theirs, admins included.
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(Staff.UserId);

        var act = () => Handler.Handle(new EditPathwayNoteCommand(pathway.Id, note.Id, Edited, 1, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NoteWithNoKnownAuthor_ThrowsForbidden()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = new PatientPathwayNote { Id = Guid.NewGuid(), PatientPathwayId = pathway.Id, AuthorUserId = null, Text = NoteText, Version = 1 };
        pathway.Notes.Add(note);

        var act = () => Handler.Handle(new EditPathwayNoteCommand(pathway.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Test]
    public async Task Handle_StaleVersion_ThrowsConflictWithoutSaving()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(Staff.UserId, version: 3);

        var act = () => Handler.Handle(new EditPathwayNoteCommand(pathway.Id, note.Id, Edited, 2, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PathwayNotes.ChangedMeanwhileMessage);
        note.Version.Should().Be(3);
        VerifyNotSaved();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public async Task Handle_PathwayClosed_ThrowsConflict(Status status)
    {
        var pathway = GivenPathway(CreatePathway(status));
        var note = pathway.AddNote(Staff.UserId);

        var act = () => Handler.Handle(new EditPathwayNoteCommand(pathway.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Notes can't be added or edited while the pathway is *");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_PatientAnonymised_ThrowsConflict()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.AnonymisedAtUtc = Now.AddDays(-1);
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: patient));
        var note = pathway.AddNote(Staff.UserId);

        var act = () => Handler.Handle(new EditPathwayNoteCommand(pathway.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_DeletedNote_ThrowsConflictWithoutSaving()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(Staff.UserId);
        note.DeletedAtUtc = Now.AddDays(-1);

        var act = () => Handler.Handle(new EditPathwayNoteCommand(pathway.Id, note.Id, Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PathwayNotes.DeletedMessage);
        note.Text.Should().Be(NoteText);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NoteNotOnThePathway_ThrowsNotFound()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(new EditPathwayNoteCommand(pathway.Id, Guid.NewGuid(), Edited, 1, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(Staff.UserId);
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new EditPathwayNoteCommand(pathway.Id, note.Id, Edited, 1, Staff), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(pathway.Id, cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }
}
