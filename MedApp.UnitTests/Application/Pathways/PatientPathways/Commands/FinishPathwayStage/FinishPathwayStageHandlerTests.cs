using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;
using System.Text.Json;
using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;

[TestFixture]
public sealed class FinishPathwayStageHandlerTests : PathwayHandlerTestBase
{
    private FinishPathwayStageHandler Handler => Fixture.Create<FinishPathwayStageHandler>();

    [Test]
    public async Task Handle_PathwayBlocked_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.Blocked));

        var act = () => Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*on hold*");
    }

    [Test]
    public async Task Handle_CurrentStage_FinishesAndLocksItAndMovesOn()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));

        var response = await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        response.Stages[0].State.Should().Be("Completed");
        response.Stages[0].IsLocked.Should().BeTrue();
        response.Stages[0].CompletedByUsername.Should().Be(Staff.Username);
        response.CurrentStageId.Should().Be(pathway.StageAt(2).Id);
        response.Status.Should().Be("InProgress");
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_LastStage_MovesThePathwayToReview()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 2));

        var response = await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(3).Id, Staff), CancellationToken.None);

        response.Status.Should().Be("InReview");
        response.CurrentStageId.Should().BeNull();
        response.AllowedStatusChanges.Should().Contain("Completed");
    }

    private PatientPathwayAuditEntry FinishedEntry() =>
        SavedAudit.Single(e => e.Action == PatientPathwayAuditAction.StageFinished);

    /// <summary>An InProgress pathway whose first stage involves a visit as given.</summary>
    private PatientPathway GivenFirstStageWithVisit(VisitRequirement requirement)
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.StageAt(1).VisitRequirement = requirement;
        return GivenPathway(pathway);
    }

    [Test]
    public async Task Handle_RequiredVisitNotAttendedByStaff_ThrowsConflictWithoutSaving()
    {
        var pathway = GivenFirstStageWithVisit(VisitRequirement.Required);

        var act = () => Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"The '{ConsultationStage}' stage needs a visit before it can be finished*An admin can finish it without one.");
        VerifyNotSaved();
        pathway.StageAt(1).IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_RequiredVisitStaffAskingToFinishWithoutIt_ThrowsConflict()
    {
        var pathway = GivenFirstStageWithVisit(VisitRequirement.Required);

        var act = () => Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, WithoutVisit: true, Reason: WithoutVisitReason),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*needs a visit*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_RequiredVisitAdminWithoutSayingWithoutVisit_ThrowsConflict()
    {
        var pathway = GivenFirstStageWithVisit(VisitRequirement.Required);

        var act = () => Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Admin, CanOverrideStages: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*needs a visit*");
        VerifyNotSaved();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Handle_RequiredVisitAdminWithoutVisitButNoReason_ThrowsValidationException(string? reason)
    {
        var pathway = GivenFirstStageWithVisit(VisitRequirement.Required);

        var act = () => Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Admin, WithoutVisit: true, Reason: reason, CanOverrideStages: true),
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(FinishPathwayStageCommand.Reason)
            && e.ErrorMessage == FinishPathwayStageHandler.ReasonRequired);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_RequiredVisitAdminWithoutVisitAndAReason_FinishesAndRecordsTheBypass()
    {
        var pathway = GivenFirstStageWithVisit(VisitRequirement.Required);

        var response = await Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Admin, WithoutVisit: true, Reason: $"  {WithoutVisitReason} ", CanOverrideStages: true),
            CancellationToken.None);

        response.Stages[0].State.Should().Be("Completed");
        var details = JsonDocument.Parse(FinishedEntry().Details).RootElement;
        details.GetProperty("withoutVisit").GetBoolean().Should().BeTrue();
        details.GetProperty("visitRequirement").GetString().Should().Be("Required");
        details.GetProperty("reason").GetString().Should().Be(WithoutVisitReason);
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_RequiredVisitAttended_StaffFinishesWithNothingRecordedAboutTheVisit()
    {
        var pathway = GivenFirstStageWithVisit(VisitRequirement.Required);
        GivenStageAppointments(CreateStageAppointment(pathway, pathway.StageAt(1), AppointmentStatus.Attended));

        var response = await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        response.Stages[0].State.Should().Be("Completed");
        FinishedEntry().Details.Should().Be("{}");
    }

    [TestCase(AppointmentStatus.Booked)]
    [TestCase(AppointmentStatus.DidNotAttend)]
    [TestCase(AppointmentStatus.Cancelled)]
    public async Task Handle_RequiredVisitNotAttended_ThrowsConflictWhateverElseItHas(AppointmentStatus status)
    {
        var pathway = GivenFirstStageWithVisit(VisitRequirement.Required);
        GivenStageAppointments(CreateStageAppointment(pathway, pathway.StageAt(1), status));

        var act = () => Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_SuggestedVisitNotAttended_StaffFinishesAndItIsRecorded()
    {
        var pathway = GivenFirstStageWithVisit(VisitRequirement.Suggested);

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        FinishedEntry().Details.Should().Be("{\"withoutVisit\":true,\"visitRequirement\":\"Suggested\"}");
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_SuggestedVisitWithAReason_KeepsTheReason()
    {
        var pathway = GivenFirstStageWithVisit(VisitRequirement.Suggested);

        await Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, WithoutVisit: true, Reason: WithoutVisitReason),
            CancellationToken.None);

        JsonDocument.Parse(FinishedEntry().Details).RootElement.GetProperty("reason").GetString().Should().Be(WithoutVisitReason);
    }

    [Test]
    public async Task Handle_NoVisit_FinishesAsBeforeWithoutTouchingAppointments()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        FinishedEntry().Details.Should().Be("{}");
        SavedChanges.Single().NewAppointments.Should().BeEmpty();
        SavedChanges.Single().AppointmentEvents.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_RequiredStageFinishedAgainAfterAnUnlock_DoesNotAskForTheVisitAgain()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 3);
        var stage = pathway.StageAt(2);
        stage.VisitRequirement = VisitRequirement.Required;
        stage.IsLocked = false;
        GivenPathway(pathway);

        var response = await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, stage.Id, Staff), CancellationToken.None);

        response.Status.Should().Be("InReview");
        FinishedEntry().Details.Should().Be("{}");
    }

    [Test]
    public async Task Handle_StageWithARequestedVisit_CancelsItButLeavesBookedOnes()
    {
        var pathway = GivenFirstStageWithVisit(VisitRequirement.Suggested);
        var requested = CreateStageAppointment(pathway, pathway.StageAt(1));
        var booked = CreateStageAppointment(pathway, pathway.StageAt(1), AppointmentStatus.Booked);
        GivenStageAppointments(requested, booked);

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        requested.Status.Should().Be(AppointmentStatus.Cancelled);
        requested.CancelReason.Should().Be(PathwayChange.StageFinishedReason);
        booked.Status.Should().Be(AppointmentStatus.Booked);
        var cancelled = SavedChanges.Single().AppointmentEvents.Should().ContainSingle().Subject;
        cancelled.AppointmentId.Should().Be(requested.Id);
        cancelled.Action.Should().Be(AppointmentEventAction.Cancelled);
    }

    [Test]
    public async Task Handle_NextStageInvolvesAVisit_AsksForIt()
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.StageAt(2).VisitRequirement = VisitRequirement.Required;
        GivenPathway(pathway);

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        var visit = SavedChanges.Single().NewAppointments.Should().ContainSingle().Subject;
        visit.StageId.Should().Be(pathway.StageAt(2).Id);
        visit.PathwayId.Should().Be(pathway.Id);
        visit.Status.Should().Be(AppointmentStatus.Requested);
        visit.IsAutomatic.Should().BeTrue();
        SavedChanges.Single().AppointmentEvents.Should().ContainSingle(e => e.Action == AppointmentEventAction.Requested);
    }

    [Test]
    public async Task Handle_NextStageAlreadyBookedAhead_AsksForNothingMore()
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.StageAt(2).VisitRequirement = VisitRequirement.Required;
        GivenPathway(pathway);
        GivenStageAppointments(CreateStageAppointment(pathway, pathway.StageAt(2), AppointmentStatus.Booked));

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        SavedChanges.Single().NewAppointments.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NotTheLastStage_LoadsTheAppointmentsOfItAndTheNextStageAndPassesTheToken()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), cts.Token);

        Appointments.Verify(r => r.GetForStagesForUpdateAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { pathway.StageAt(1).Id, pathway.StageAt(2).Id })),
            cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }

    [Test]
    public async Task Handle_TheLastStage_LoadsOnlyItsOwnAppointments()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 2));

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(3).Id, Staff), CancellationToken.None);

        Appointments.Verify(r => r.GetForStagesForUpdateAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { pathway.StageAt(3).Id })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_StageAlreadyFinishedAndLocked_ThrowsConflictWithoutSaving()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 1));

        var act = () => Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_BranchPointWithoutABranch_ThrowsValidationNamingTheBranchesWithoutSaving()
    {
        var pathway = GivenPathway(CreateBranchingPathway());

        var act = () => Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "Branch"
                                                      && e.ErrorMessage.Contains($"{BenignBranch}, {TreatmentBranch}"));
        VerifyNotSaved();
        pathway.StageAt(1).IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_BranchPointWithAnUnknownBranch_ThrowsValidation()
    {
        var pathway = GivenPathway(CreateBranchingPathway());

        var act = () => Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Branch: "Surgery"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*'Surgery' isn't one of the branches*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_BranchGivenForAStageThatIsntABranchPoint_ThrowsValidation()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Branch: TreatmentBranch), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*isn't a branch point*");
    }

    [Test]
    public async Task Handle_BranchChosen_SkipsTheOtherBranchesStagesAndMovesToTheChosenOne()
    {
        var pathway = GivenPathway(CreateBranchingPathway());

        var response = await Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Branch: "treatment"), CancellationToken.None);

        response.CurrentStageId.Should().Be(pathway.StageAt(3).Id);
        response.Stages.Select(s => s.State).Should().Equal("Completed", "Skipped", "NotStarted", "NotStarted", "NotStarted");
        response.Stages[1].SkipReason.Should().Be(nameof(StageSkipReason.BranchNotChosen));
        response.SkippedStageCount.Should().Be(1);

        var chosen = SavedAudit.Single(e => e.Action == PatientPathwayAuditAction.BranchChosen);
        chosen.StageId.Should().Be(pathway.StageAt(1).Id);
        using var details = JsonDocument.Parse(chosen.Details);
        details.RootElement.GetProperty("branch").GetString().Should().Be(TreatmentBranch);
        details.RootElement.GetProperty("skippedStageIds").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(pathway.StageAt(2).Id);
        details.RootElement.GetProperty("skippedStages").EnumerateArray().Select(e => e.GetString()).Should().Equal(BenignStage);
        SavedAudit.Select(e => e.Action).Should().ContainInOrder(PatientPathwayAuditAction.StageFinished, PatientPathwayAuditAction.BranchChosen);
    }

    [Test]
    public async Task Handle_BenignChosen_SkipsBothTreatmentStagesAndLeavesTheStageThatAlwaysRuns()
    {
        var pathway = GivenPathway(CreateBranchingPathway());

        var response = await Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Branch: BenignBranch), CancellationToken.None);

        response.Stages.Select(s => s.State).Should().Equal("Completed", "NotStarted", "Skipped", "Skipped", "NotStarted");
        response.CurrentStageId.Should().Be(pathway.StageAt(2).Id);
    }

    [Test]
    public async Task Handle_BranchChosen_CancelsTheSkippedStagesRequestedVisitsAndAsksForTheNextOnes()
    {
        var pathway = GivenPathway(CreateBranchingPathway());
        pathway.StageAt(3).VisitRequirement = VisitRequirement.Required;
        var aheadOnBenign = new Appointment { Id = Guid.NewGuid(), StageId = pathway.StageAt(2).Id, Status = AppointmentStatus.Requested };
        GivenStageAppointments(aheadOnBenign);

        await Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Branch: TreatmentBranch), CancellationToken.None);

        aheadOnBenign.Status.Should().Be(AppointmentStatus.Cancelled);
        aheadOnBenign.CancelReason.Should().Be(PathwayChange.StageSkippedReason);
        SavedChanges.Single().NewAppointments.Should().ContainSingle().Which.StageId.Should().Be(pathway.StageAt(3).Id);
        Appointments.Verify(r => r.GetForStagesForUpdateAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { pathway.StageAt(1).Id, pathway.StageAt(2).Id, pathway.StageAt(3).Id })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_BranchPointFinishedAgainAfterAnUnlock_AsksForNoBranchAndRefusesOne()
    {
        var pathway = CreateBranchingPathway(finishedStages: 1);
        Skip(pathway.StageAt(2), StageSkipReason.BranchNotChosen);
        pathway.StageAt(1).IsLocked = false;
        GivenPathway(pathway);

        var withBranch = () => Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Branch: BenignBranch), CancellationToken.None);
        await withBranch.Should().ThrowAsync<ValidationException>().WithMessage("*reinstate its stages*");

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        pathway.StageAt(1).IsLocked.Should().BeTrue();
        pathway.StageAt(2).IsSkipped.Should().BeTrue();
        SavedAudit.Should().NotContain(e => e.Action == PatientPathwayAuditAction.BranchChosen);
    }

    [Test]
    public async Task Handle_SkippedStage_ThrowsConflictSayingItCanBeReinstated()
    {
        var pathway = CreateBranchingPathway(finishedStages: 1);
        Skip(pathway.StageAt(2), StageSkipReason.BranchNotChosen);
        GivenPathway(pathway);

        var act = () => Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*was skipped*reinstate*");
    }

    [Test]
    public async Task Handle_LastStageAfterSkippedOnes_MovesThePathwayToReview()
    {
        var pathway = CreateBranchingPathway(finishedStages: 2);
        Skip(pathway.StageAt(3), StageSkipReason.BranchNotChosen);
        Skip(pathway.StageAt(4), StageSkipReason.BranchNotChosen);
        GivenPathway(pathway);

        var response = await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(5).Id, Staff), CancellationToken.None);

        response.Status.Should().Be(nameof(Status.InReview));
    }
}
