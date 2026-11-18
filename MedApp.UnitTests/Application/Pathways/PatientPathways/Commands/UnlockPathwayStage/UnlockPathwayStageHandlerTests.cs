using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.UnlockPathwayStage;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.UnlockPathwayStage;

[TestFixture]
public sealed class UnlockPathwayStageHandlerTests : PathwayHandlerTestBase
{
    private UnlockPathwayStageHandler Handler => Fixture.Create<UnlockPathwayStageHandler>();

    [Test]
    public async Task Handle_StageNotFinished_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 1));

        var act = () => Handler.Handle(new UnlockPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*nothing to unlock*");
    }

    [Test]
    public async Task Handle_PathwayCancelled_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.Cancelled, finishedStages: 1));

        var act = () => Handler.Handle(new UnlockPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*cancelled*");
    }

    [Test]
    public async Task Handle_FinishedStageOfACompletedPathway_UnlocksItAndReopensThePathway()
    {
        var pathway = GivenPathway(CreatePathway(Status.Completed, finishedStages: 3));

        var response = await Handler.Handle(new UnlockPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, Admin), CancellationToken.None);

        response.Status.Should().Be("InProgress");
        response.Stages[1].IsLocked.Should().BeFalse();
        response.Stages[1].CanEdit.Should().BeTrue();
        response.Stages[0].CanEdit.Should().BeFalse();
        SavedAudit.Select(e => e.Action).Should().Equal(PatientPathwayAuditAction.StageUnlocked, PatientPathwayAuditAction.StatusChanged);
        VerifySavedOnce();
    }
}
