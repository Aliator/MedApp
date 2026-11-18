using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.PatientPathways.Commands.ReinstatePathwayStage;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.ReinstatePathwayStage;

[TestFixture]
public sealed class ReinstatePathwayStageHandlerTests : PathwayHandlerTestBase
{
    private ReinstatePathwayStageHandler Handler => Fixture.Create<ReinstatePathwayStageHandler>();

    [Test]
    public async Task Handle_StageBeforeTheCurrentOne_BecomesCurrentAgain()
    {
        var pathway = CreatePathway(Status.InProgress);
        Skip(pathway.StageAt(1), StageSkipReason.PatientDeclined);
        GivenPathway(pathway);

        var response = await Handler.Handle(
            new ReinstatePathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, $" {SkipDetail} ", Admin), CancellationToken.None);

        response.CurrentStageId.Should().Be(pathway.StageAt(1).Id);
        response.Stages[0].State.Should().Be("NotStarted");
        response.Stages[0].SkipReason.Should().BeNull();
        response.Stages[0].SkippedAtUtc.Should().BeNull();
        response.SkippedStageCount.Should().Be(0);
        VerifySavedOnce();

        var entry = SavedAudit.Single(e => e.Action == PatientPathwayAuditAction.StageReinstated);
        using var details = JsonDocument.Parse(entry.Details);
        details.RootElement.GetProperty("reason").GetString().Should().Be(SkipDetail);
        details.RootElement.GetProperty("skipReason").GetString().Should().Be("PatientDeclined");
    }

    [Test]
    public async Task Handle_PathwayInReview_GoesBackInProgress()
    {
        var pathway = CreatePathway(Status.InReview, finishedStages: 2);
        Skip(pathway.StageAt(3));
        GivenPathway(pathway);

        var response = await Handler.Handle(
            new ReinstatePathwayStageCommand(pathway.Id, pathway.StageAt(3).Id, SkipDetail, Admin), CancellationToken.None);

        response.Status.Should().Be(nameof(Status.InProgress));
        response.CurrentStageId.Should().Be(pathway.StageAt(3).Id);
    }

    [Test]
    public async Task Handle_ReinstatedStageInvolvesAVisit_AsksForIt()
    {
        var pathway = CreatePathway(Status.InProgress);
        Skip(pathway.StageAt(1));
        pathway.StageAt(1).VisitRequirement = VisitRequirement.Suggested;
        GivenPathway(pathway);

        await Handler.Handle(
            new ReinstatePathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, SkipDetail, Admin), CancellationToken.None);

        SavedChanges.Single().NewAppointments.Should().ContainSingle()
            .Which.Should().Match<Appointment>(a => a.StageId == pathway.StageAt(1).Id && a.Status == AppointmentStatus.Requested);
    }

    [Test]
    public async Task Handle_ReinstatedStageAfterTheCurrentOne_AsksForNoVisitYet()
    {
        var pathway = CreateBranchingPathway(finishedStages: 1);
        Skip(pathway.StageAt(2), StageSkipReason.BranchNotChosen);
        pathway.StageAt(2).VisitRequirement = VisitRequirement.Required;
        GivenPathway(pathway);

        var response = await Handler.Handle(
            new ReinstatePathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, SkipDetail, Admin), CancellationToken.None);

        // The Benign letter is before Treatment, so it's current again; it involves a visit, which is asked for.
        response.CurrentStageId.Should().Be(pathway.StageAt(2).Id);
        SavedChanges.Single().NewAppointments.Should().ContainSingle();
    }

    [Test]
    public async Task Handle_LaterStageReinstated_LeavesTheCurrentStage()
    {
        var pathway = CreateBranchingPathway(finishedStages: 1);
        Skip(pathway.StageAt(3), StageSkipReason.BranchNotChosen);
        Skip(pathway.StageAt(4), StageSkipReason.BranchNotChosen);
        pathway.StageAt(3).VisitRequirement = VisitRequirement.Required;
        GivenPathway(pathway);

        var response = await Handler.Handle(
            new ReinstatePathwayStageCommand(pathway.Id, pathway.StageAt(4).Id, SkipDetail, Admin), CancellationToken.None);

        response.CurrentStageId.Should().Be(pathway.StageAt(2).Id);
        SavedChanges.Single().NewAppointments.Should().BeEmpty();
        Appointments.Verify(r => r.GetForStagesForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_StageNotSkipped_ThrowsConflictWithoutSaving()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(
            new ReinstatePathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, SkipDetail, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*wasn't skipped*");
        VerifyNotSaved();
    }

    [TestCase(Status.Blocked)]
    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public async Task Handle_PathwayNotOpenOrDischarged_ThrowsConflict(Status status)
    {
        var pathway = CreatePathway(status);
        Skip(pathway.StageAt(1));
        GivenPathway(pathway);

        var act = () => Handler.Handle(
            new ReinstatePathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, SkipDetail, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Stages can't be reinstated while*");
        VerifyNotSaved();
    }
}
