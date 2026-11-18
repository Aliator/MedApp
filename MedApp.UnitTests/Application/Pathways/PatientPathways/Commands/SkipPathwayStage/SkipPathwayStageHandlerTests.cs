using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.SkipPathwayStage;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.SkipPathwayStage;

[TestFixture]
public sealed class SkipPathwayStageHandlerTests : PathwayHandlerTestBase
{
    private SkipPathwayStageHandler Handler => Fixture.Create<SkipPathwayStageHandler>();

    /// <summary>An InProgress pathway whose first stage is optional.</summary>
    private PatientPathway GivenOptionalFirstStage(Status status = Status.InProgress)
    {
        var pathway = CreatePathway(status);
        pathway.StageAt(1).IsOptional = true;
        return GivenPathway(pathway);
    }

    [Test]
    public async Task Handle_OptionalCurrentStage_SkipsItAndMovesOn()
    {
        var pathway = GivenOptionalFirstStage();

        var response = await Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, "patientdeclined", null, Staff), CancellationToken.None);

        var stage = response.Stages[0];
        stage.State.Should().Be(PathwayWorkflow.StageSkipped);
        stage.SkipReason.Should().Be(nameof(StageSkipReason.PatientDeclined));
        stage.SkippedByUsername.Should().Be(Staff.Username);
        stage.SkippedAtUtc.Should().Be(Now);
        stage.IsCurrent.Should().BeFalse();
        response.CurrentStageId.Should().Be(pathway.StageAt(2).Id);
        response.SkippedStageCount.Should().Be(1);
        response.CompletedStageCount.Should().Be(0);
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_Skipped_AuditsTheReasonByNameAndTheDetail()
    {
        var pathway = GivenOptionalFirstStage();

        await Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, "Other", $"  {SkipDetail}  ", Staff), CancellationToken.None);

        var entry = SavedAudit.Single(e => e.Action == PatientPathwayAuditAction.StageSkipped);
        entry.StageId.Should().Be(pathway.StageAt(1).Id);
        using var details = JsonDocument.Parse(entry.Details);
        details.RootElement.GetProperty("skipReason").GetString().Should().Be("Other");
        details.RootElement.GetProperty("detail").GetString().Should().Be(SkipDetail);
        pathway.StageAt(1).SkipDetail.Should().Be(SkipDetail);
    }

    [Test]
    public async Task Handle_UnstartedPathway_GoesInProgress()
    {
        var pathway = GivenOptionalFirstStage(Status.Assigned);

        var response = await Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, "NotNeeded", null, Staff), CancellationToken.None);

        response.Status.Should().Be(nameof(Status.InProgress));
    }

    [Test]
    public async Task Handle_LastStageToDo_MovesThePathwayToReview()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 2);
        pathway.StageAt(3).IsOptional = true;
        GivenPathway(pathway);

        var response = await Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, pathway.StageAt(3).Id, "NotNeeded", null, Staff), CancellationToken.None);

        response.Status.Should().Be(nameof(Status.InReview));
        response.CurrentStageId.Should().BeNull();
    }

    [Test]
    public async Task Handle_StageNotOptional_ThrowsConflictWithoutSaving()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, "NotNeeded", null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*isn't optional*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_OptionalStageNotYetCurrent_ThrowsConflict()
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.StageAt(2).IsOptional = true;
        GivenPathway(pathway);

        var act = () => Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, "NotNeeded", null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*finish the '{ConsultationStage}' stage first*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_PathwayOnHold_ThrowsConflict()
    {
        var pathway = GivenOptionalFirstStage(Status.Blocked);

        var act = () => Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, "NotNeeded", null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*on hold*");
    }

    [Test]
    public async Task Handle_StageWithARequestedVisit_CancelsItButLeavesBookedOnes()
    {
        var pathway = GivenOptionalFirstStage();
        var requested = new Appointment { Id = Guid.NewGuid(), StageId = pathway.StageAt(1).Id, Status = AppointmentStatus.Requested };
        var booked = new Appointment { Id = Guid.NewGuid(), StageId = pathway.StageAt(1).Id, Status = AppointmentStatus.Booked };
        GivenStageAppointments(requested, booked);

        await Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, "NotNeeded", null, Staff), CancellationToken.None);

        requested.Status.Should().Be(AppointmentStatus.Cancelled);
        requested.CancelReason.Should().Be(PathwayChange.StageSkippedReason);
        booked.Status.Should().Be(AppointmentStatus.Booked);
    }

    [Test]
    public async Task Handle_NextStageInvolvesAVisit_AsksForIt()
    {
        var pathway = GivenOptionalFirstStage();
        pathway.StageAt(2).VisitRequirement = VisitRequirement.Required;

        await Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, "NotNeeded", null, Staff), CancellationToken.None);

        SavedChanges.Single().NewAppointments.Should().ContainSingle()
            .Which.StageId.Should().Be(pathway.StageAt(2).Id);
    }

    [Test]
    public async Task Handle_LoadsTheAppointmentsOfItAndTheNextStageAndPassesTheToken()
    {
        var pathway = GivenOptionalFirstStage();
        using var cts = new CancellationTokenSource();

        await Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, "NotNeeded", null, Staff), cts.Token);

        Appointments.Verify(r => r.GetForStagesForUpdateAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { pathway.StageAt(1).Id, pathway.StageAt(2).Id })),
            cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }

    [Test]
    public async Task Handle_UnknownStage_ThrowsNotFound()
    {
        var pathway = GivenOptionalFirstStage();

        var act = () => Handler.Handle(
            new SkipPathwayStageCommand(pathway.Id, Guid.NewGuid(), "NotNeeded", null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
