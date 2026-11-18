using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.StartPathwayStage;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.StartPathwayStage;

[TestFixture]
public sealed class StartPathwayStageHandlerTests : PathwayHandlerTestBase
{
    private StartPathwayStageHandler Handler => Fixture.Create<StartPathwayStageHandler>();

    [Test]
    public async Task Handle_StageNotOnThePathway_ThrowsNotFound()
    {
        var pathway = GivenPathway(CreatePathway());

        var act = () => Handler.Handle(new StartPathwayStageCommand(pathway.Id, Guid.NewGuid(), Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_StageAfterTheCurrentOne_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway());

        var act = () => Handler.Handle(new StartPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*in order*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_StageAlreadyStarted_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        pathway.StageAt(1).StartedAtUtc = Now.AddHours(-1);

        var act = () => Handler.Handle(new StartPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already been started*");
    }

    [Test]
    public async Task Handle_CurrentStage_StartsItAndSaves()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));

        var response = await Handler.Handle(new StartPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        response.Status.Should().Be("InProgress");
        response.Stages[0].State.Should().Be("InProgress");
        response.Stages[0].StartedByUsername.Should().Be(Staff.Username);
        VerifySavedOnce();
    }
}
