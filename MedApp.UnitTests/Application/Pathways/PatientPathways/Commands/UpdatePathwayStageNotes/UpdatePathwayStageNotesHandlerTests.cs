using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;

[TestFixture]
public sealed class UpdatePathwayStageNotesHandlerTests : PathwayHandlerTestBase
{
    private UpdatePathwayStageNotesHandler Handler => Fixture.Create<UpdatePathwayStageNotesHandler>();

    [Test]
    public async Task Handle_FinishedLockedStage_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 1));

        var act = () => Handler.Handle(
            new UpdatePathwayStageNotesCommand(pathway.Id, pathway.StageAt(1).Id, "Changed", Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*locked*");
        pathway.StageAt(1).Notes.Should().Be("Done.");
    }

    [Test]
    public async Task Handle_CurrentStage_SavesTheNotes()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 1));
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(
            new UpdatePathwayStageNotesCommand(pathway.Id, pathway.StageAt(2).Id, "Went well.", Staff), cts.Token);

        response.Stages[1].Notes.Should().Be("Went well.");
        SavedAudit.Should().Contain(e => e.Action == PatientPathwayAuditAction.StageNotesUpdated && e.StageId == pathway.StageAt(2).Id);
        VerifySavedOnce(cts.Token);
    }

    [Test]
    public async Task Handle_FinishedStageUnlockedByAnAdmin_SavesTheNotes()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 2));
        pathway.StageAt(1).IsLocked = false;

        await Handler.Handle(new UpdatePathwayStageNotesCommand(pathway.Id, pathway.StageAt(1).Id, "Corrected.", Staff), CancellationToken.None);

        pathway.StageAt(1).Notes.Should().Be("Corrected.");
        pathway.StageAt(1).IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task Handle_NotesUnchanged_DoesNotSave()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));

        await Handler.Handle(new UpdatePathwayStageNotesCommand(pathway.Id, pathway.StageAt(1).Id, string.Empty, Staff), CancellationToken.None);

        VerifyNotSaved();
    }
}
