using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.PickUpPathwayStage;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.PickUpPathwayStage;

[TestFixture]
public sealed class PickUpPathwayStageHandlerTests : PathwayHandlerTestBase
{
    private static readonly Guid Lab = Guid.Parse("0000005a-0000-0000-0000-0000000000bb");

    private PickUpPathwayStageHandler Handler => Fixture.Create<PickUpPathwayStageHandler>();

    private static PathwayActor LabStaff => Staff with { Scope = new TeamScope(Staff.UserId, [Lab], false) };

    /// <summary>A chain of three whose second stage is the lab's, current and handed over unless said otherwise.</summary>
    private PatientPathway GivenHandedToTheLab(bool handedOver = true, int finishedStages = 1)
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: finishedStages);
        pathway.TeamId = TeamId;
        var stage = pathway.StageAt(2);
        stage.OwnerTeamId = Lab;
        stage.ReachedAtUtc = Now.AddHours(-2);
        if (handedOver)
            stage.HandedOverFromTeamId = TeamId;
        return GivenPathway(pathway);
    }

    [Test]
    public async Task Handle_AStageHandedToTheLab_PicksItUpForTheLabAndAuditsIt()
    {
        var pathway = GivenHandedToTheLab();
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(new PickUpPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, LabStaff), cts.Token);

        pathway.StageAt(2).PickedUpAtUtc.Should().Be(Now);
        pathway.StageAt(2).PickedUpByUserId.Should().Be(Staff.UserId);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.StagePickedUp && e.StageId == pathway.StageAt(2).Id);
        response.Stages[1].PickedUpByUsername.Should().Be(Staff.Username);
        response.Stages[1].CanPickUp.Should().BeFalse();
        // The lab works its stage on the clinic's pathway, which stays the clinic's.
        response.Stages[1].CanEdit.Should().BeTrue();
        response.ReadOnlyReason.Should().BeNull();
        response.AllowedStatusChanges.Should().BeEmpty();
        VerifySavedOnce(cts.Token);
    }

    [Test]
    public async Task Handle_PickedUpAlready_ChangesNothing()
    {
        var pathway = GivenHandedToTheLab();
        pathway.StageAt(2).PickedUpAtUtc = Now.AddHours(-1);

        await Handler.Handle(new PickUpPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, LabStaff), CancellationToken.None);

        pathway.StageAt(2).PickedUpAtUtc.Should().Be(Now.AddHours(-1));
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NotHandedOver_IsAConflict()
    {
        var pathway = GivenHandedToTheLab(handedOver: false);

        var act = () => Handler.Handle(new PickUpPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, LabStaff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PickUpPathwayStageHandler.NotHandedOverMessage);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NotCurrent_IsAConflict()
    {
        var pathway = GivenHandedToTheLab(finishedStages: 0);

        var act = () => Handler.Handle(new PickUpPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, LabStaff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PickUpPathwayStageHandler.NotCurrentMessage);
    }

    [Test]
    public async Task Handle_OnHold_IsAConflict()
    {
        var pathway = GivenHandedToTheLab();
        pathway.Status = Status.Blocked;

        var act = () => Handler.Handle(new PickUpPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, LabStaff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PathwayBranches.OnHoldOrClosedMessage);
    }

    [Test]
    public async Task Handle_ThePathwaysOwnTeamOnTheLabsStage_IsAConflictNamingWhoseStageItIs()
    {
        var pathway = GivenHandedToTheLab();
        var clinic = Staff with { Scope = new TeamScope(Staff.UserId, [TeamId], false) };

        var act = () => Handler.Handle(new PickUpPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, clinic), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"The '{PathwayStage}' stage is*to work*");
        VerifyNotSaved();
    }
}
