using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayNote;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.AddPathwayNote;

[TestFixture]
public sealed class AddPathwayNoteHandlerTests : PathwayHandlerTestBase
{
    private AddPathwayNoteHandler Handler => Fixture.Create<AddPathwayNoteHandler>();

    [Test]
    public async Task Handle_PathwayDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new AddPathwayNoteCommand(Guid.NewGuid(), NoteText, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(Status.Unassigned)]
    [TestCase(Status.InProgress)]
    [TestCase(Status.Blocked)]
    [TestCase(Status.InReview)]
    [TestCase(Status.Completed)]
    public async Task Handle_NotesCanChange_AddsTheNoteAsVersionOneAndAuditsItsIdOnly(Status status)
    {
        var pathway = GivenPathway(CreatePathway(status));

        var response = await Handler.Handle(new AddPathwayNoteCommand(pathway.Id, NoteText, Staff), CancellationToken.None);

        var note = pathway.Notes.Should().ContainSingle().Subject;
        note.Should().BeEquivalentTo(new { AuthorUserId = (Guid?)Staff.UserId, Text = NoteText, CreatedAtUtc = Now, Version = 1, LastEditedAtUtc = (DateTime?)null });
        pathway.LastUpdated.Should().Be(Now);

        var change = SavedChanges.Should().ContainSingle().Subject;
        change.NewNotes.Should().Equal(note);
        change.NoteVersions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            NoteId = note.Id, Version = 1, PatientPathwayId = pathway.Id, pathway.PatientId, Text = NoteText,
            UserId = (Guid?)Staff.UserId, Staff.Username, AtUtc = Now
        });
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.NoteAdded);
        entry.Details.Should().Be($$"""{"noteId":"{{note.Id}}","version":1}""").And.NotContain(NoteText);

        response.Should().BeEquivalentTo(new
        {
            note.Id, AuthorUserId = (Guid?)Staff.UserId, AuthorUsername = Staff.Username, Text = NoteText, CreatedAtUtc = Now,
            Version = 1, CanEdit = true
        });
        VerifySavedOnce();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public async Task Handle_PathwayClosed_ThrowsConflictWithoutSaving(Status status)
    {
        var pathway = GivenPathway(CreatePathway(status));

        var act = () => Handler.Handle(new AddPathwayNoteCommand(pathway.Id, NoteText, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Notes can't be added or edited while the pathway is *");
        pathway.Notes.Should().BeEmpty();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_PatientRestricted_ThrowsConflictWithoutSaving()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: patient));

        var act = () => Handler.Handle(new AddPathwayNoteCommand(pathway.Id, NoteText, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new AddPathwayNoteCommand(pathway.Id, NoteText, Staff), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(pathway.Id, cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }
}
