using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePatientPathway;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.UpdatePatientPathway;

[TestFixture]
public sealed class UpdatePatientPathwayHandlerTests : PathwayHandlerTestBase
{
    private UpdatePatientPathwayHandler Handler => Fixture.Create<UpdatePatientPathwayHandler>();

    [Test]
    public async Task Handle_PathwayDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new UpdatePatientPathwayCommand(Guid.NewGuid(), "x", null, null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public async Task Handle_PathwayClosedOff_ThrowsConflict(Status status)
    {
        var pathway = GivenPathway(CreatePathway(status));

        var act = () => Handler.Handle(new UpdatePatientPathwayCommand(pathway.Id, "x", null, null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_FieldsChanged_UpdatesThemAndAuditsWhatChanged()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var command = new UpdatePatientPathwayCommand(pathway.Id, " New title ", Today.AddDays(20), "Urgent", Staff);

        var response = await Handler.Handle(command, CancellationToken.None);

        pathway.Title.Should().Be("New title");
        pathway.Priority.Should().Be(PatientPathwayPriority.Urgent);
        response.DueDate.Should().Be(Today.AddDays(20));
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.DetailsUpdated);
        entry.Details.Should().Contain("\"title\"").And.Contain("\"priority\"").And.Contain("\"dueDate\"")
            .And.NotContain("\"notes\"");
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_SameTitleWithSpaces_ChangesNothingAndDoesNotSave()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));
        var command = new UpdatePatientPathwayCommand(pathway.Id, $"  {PathwayTitle}  ", null, null, Staff);

        await Handler.Handle(command, CancellationToken.None);

        pathway.Title.Should().Be(PathwayTitle);
        pathway.LastUpdated.Should().Be(Now.AddDays(-7));
        SavedAudit.Should().BeEmpty();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NothingChanged_DoesNotSave()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));
        var command = new UpdatePatientPathwayCommand(pathway.Id, PathwayTitle, null, "medium", Staff);

        await Handler.Handle(command, CancellationToken.None);

        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_Edited_ResponseNotesCanBeEditedOnlyByTheirAuthor()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var own = pathway.AddNote(Staff.UserId);
        pathway.AddNote(OtherUserId, createdAt: Now.AddDays(-3));

        var response = await Handler.Handle(new UpdatePatientPathwayCommand(pathway.Id, "Renamed", null, null, Staff), CancellationToken.None);

        response.NoteList.Should().HaveCount(2);
        response.NoteList.Single(n => n.CanEdit).Id.Should().Be(own.Id);
        response.NoteList.Single(n => !n.CanEdit).AuthorUsername.Should().Be(OtherUsername);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new UpdatePatientPathwayCommand(pathway.Id, "Renamed", null, null, Staff), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(pathway.Id, cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }
}
