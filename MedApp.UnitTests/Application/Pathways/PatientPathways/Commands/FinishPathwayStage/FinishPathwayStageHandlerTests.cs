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
            .WithMessage($"The '{ConsultationStage}' stage needs a visit before it can be finished*Someone allowed to finish a stage without its visit, such as a senior clinician, can finish it without one.");
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
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Admin, CanFinishWithoutVisit: true),
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
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Admin, WithoutVisit: true, Reason: reason, CanFinishWithoutVisit: true),
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
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Admin, WithoutVisit: true, Reason: $"  {WithoutVisitReason} ", CanFinishWithoutVisit: true),
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
    public async Task Handle_NotTheLastStage_LoadsTheAppointmentsOfItAndTheStagesStillToDoAndPassesTheToken()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), cts.Token);

        Appointments.Verify(r => r.GetForStagesForUpdateAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { pathway.StageAt(1).Id, pathway.StageAt(2).Id, pathway.StageAt(3).Id })),
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
    public async Task Handle_ChoiceNodeWithoutAnOutcome_ThrowsValidationNamingTheOutcomesWithoutSaving()
    {
        var pathway = GivenPathway(CreateBranchingPathway());

        var act = () => Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "Outcome"
                                                      && e.ErrorMessage == $"Record the outcome: {BenignBranch}, {TreatmentBranch}.");
        VerifyNotSaved();
        pathway.StageAt(1).IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ChoiceNodeWithAnUnknownOutcome_ThrowsValidation()
    {
        var pathway = GivenPathway(CreateBranchingPathway());

        var act = () => Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Outcome: "Surgery"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Record the outcome*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_OutcomeGivenForAPlainStage_ThrowsValidation()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        var act = () => Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Outcome: TreatmentBranch), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*recorded already*");
    }

    [Test]
    public async Task Handle_OutcomeRecorded_GrowsThePathwayByItsStagesOnly()
    {
        var pathway = GivenPathway(CreateBranchingPathway());

        var response = await Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Outcome: "treatment"), CancellationToken.None);

        response.Stages.Select(s => s.Name).Should().Equal(ResultsStage, TreatmentStage, FollowUpStage, DischargeStage);
        response.Stages[0].Outcome.Should().Be(TreatmentBranch);
        response.Stages[0].NeedsOutcome.Should().BeFalse();
        response.CurrentStageIds.Should().Equal(pathway.TemplateStageAt(3)!.Id);
        response.StageCount.Should().Be(4);
        response.Stages[3].PredecessorIds.Should().Equal(pathway.TemplateStageAt(4)!.Id);
        SavedChanges.Single().NewStages.Should().HaveCount(3);

        var chosen = SavedAudit.Single(e => e.Action == PatientPathwayAuditAction.BranchChosen);
        chosen.StageId.Should().Be(pathway.StageAt(1).Id);
        using var details = JsonDocument.Parse(chosen.Details);
        details.RootElement.GetProperty("outcome").GetString().Should().Be(TreatmentBranch);
        details.RootElement.GetProperty("stageIds").EnumerateArray().Should().HaveCount(3);
        SavedAudit.Select(e => e.Action).Should().ContainInOrder(PatientPathwayAuditAction.StageFinished, PatientPathwayAuditAction.BranchChosen);
    }

    [Test]
    public async Task Handle_ChoiceNodeNotRecorded_ListsItsOutcomesNonePreSelected()
    {
        var pathway = GivenPathway(CreateBranchingPathway());
        pathway.StageAt(1).StartedAtUtc = Now.AddDays(-1);

        var response = await Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Outcome: BenignBranch), CancellationToken.None);

        response.Stages.Select(s => s.Name).Should().Equal(ResultsStage, BenignStage, DischargeStage);
        response.Stages[0].ChoiceKind.Should().Be("Assessment");
        response.Stages[0].Outcomes.Select(o => o.Name).Should().Equal(BenignBranch, TreatmentBranch);
    }

    [Test]
    public async Task Handle_OutcomeRecorded_AsksForTheVisitOfTheStageItMakesCurrent()
    {
        var pathway = CreateBranchingPathway();
        pathway.Plan = pathway.Plan! with
        {
            Stages = pathway.Plan.Stages.Select(s => s.TemplateStageOrder == 3 ? s with { VisitRequirement = VisitRequirement.Required } : s).ToList()
        };
        GivenPathway(pathway);

        await Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Outcome: TreatmentBranch), CancellationToken.None);

        SavedChanges.Single().NewAppointments.Should().ContainSingle().Which.StageId.Should().Be(pathway.TemplateStageAt(3)!.Id);
    }

    [Test]
    public async Task Handle_ChoiceNodeFinishedAgainAfterAnUnlock_AsksForNoOutcomeAndRefusesOne()
    {
        var pathway = CreateBranchingPathway(outcome: BenignBranch);
        pathway.StageAt(1).IsLocked = false;
        GivenPathway(pathway);

        var withOutcome = () => Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff, Outcome: TreatmentBranch), CancellationToken.None);
        await withOutcome.Should().ThrowAsync<ValidationException>().WithMessage("*recorded already*");

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        pathway.StageAt(1).IsLocked.Should().BeTrue();
        pathway.StageAt(1).Outcome.Should().Be(BenignBranch);
        SavedAudit.Should().NotContain(e => e.Action == PatientPathwayAuditAction.BranchChosen);
    }

    [Test]
    public async Task Handle_SkippedStage_ThrowsConflictSayingItCanBeReinstated()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);
        Skip(pathway.StageAt(2));
        GivenPathway(pathway);

        var act = () => Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*was skipped*reinstate*");
    }

    [Test]
    public async Task Handle_LastStageAfterTheChosenRoute_MovesThePathwayToReview()
    {
        var pathway = CreateBranchingPathway(outcome: BenignBranch);
        Finish(pathway.TemplateStageAt(2)!);
        GivenPathway(pathway);

        var response = await Handler.Handle(
            new FinishPathwayStageCommand(pathway.Id, pathway.TemplateStageAt(5)!.Id, Staff), CancellationToken.None);

        response.Status.Should().Be(nameof(Status.InReview));
    }

    // The relay (MC7) ------------------------------------------------------------------------------------------------

    private static readonly Guid Lab = Guid.Parse("0000005a-0000-0000-0000-0000000000bb");

    /// <summary>A chain of three, its second stage the lab's; the clinic (the pathway's team) works the rest.</summary>
    private PatientPathway GivenRelay(int finishedStages = 0)
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: finishedStages);
        pathway.TeamId = TeamId;
        pathway.StageAt(2).OwnerTeamId = Lab;
        return GivenPathway(pathway);
    }

    private static PathwayActor In(Guid team) =>
        Staff with { Scope = new MedApp.Application.Common.Teams.TeamScope(Staff.UserId, [team], false) };

    [Test]
    public async Task Handle_FinishingBeforeTheLabsStage_HandsThePathwayOverToTheLab()
    {
        var pathway = GivenRelay();
        var handovers = new Mock<IPathwayHandovers>();
        Fixture.Inject(handovers.Object);

        var response = await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, In(TeamId)), CancellationToken.None);

        var stage = pathway.StageAt(2);
        stage.ReachedAtUtc.Should().Be(Now);
        stage.HandedOverFromTeamId.Should().Be(TeamId);
        var change = SavedChanges.Single();
        change.NewGrants.Should().ContainSingle(g => g.TeamId == Lab && g.Reason == MedApp.Domain.Teams.PathwayAccessReason.StageOwner);
        change.Handovers.Should().ContainSingle(h => h.ToTeamId == Lab && h.FromTeamId == TeamId);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.StageHandedOver && e.StageId == stage.Id);
        handovers.Verify(h => h.NotifyAsync(change, It.IsAny<CancellationToken>()), Times.Once);
        // The clinic reads the lab's stage, which it doesn't work.
        response.Stages[1].IsWithAnotherTeam.Should().BeTrue();
        response.Stages[1].CanEdit.Should().BeFalse();
        response.Stages[1].CanPickUp.Should().BeFalse();
    }

    [Test]
    public async Task Handle_TheClinicFinishingTheLabsStage_IsRefused()
    {
        var pathway = GivenRelay(finishedStages: 1);

        var act = () => Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, In(TeamId)), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"The '{PathwayStage}' stage is*to work*");
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_TheLabFinishingItsStage_PicksItUpAndHandsItBack()
    {
        var pathway = GivenRelay(finishedStages: 1);
        pathway.StageAt(2).ReachedAtUtc = Now.AddDays(-1);
        pathway.StageAt(2).HandedOverFromTeamId = TeamId;

        await Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(2).Id, In(Lab)), CancellationToken.None);

        pathway.StageAt(2).PickedUpAtUtc.Should().Be(Now);
        pathway.StageAt(3).HandedOverFromTeamId.Should().Be(Lab);
        SavedAudit.Select(e => e.Action).Should().ContainInOrder(
            PatientPathwayAuditAction.StagePickedUp, PatientPathwayAuditAction.StageFinished, PatientPathwayAuditAction.StageHandedOver);
        // Back with the clinic: no grant for its own team.
        SavedChanges.Single().NewGrants.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AnotherClinicsStage_IsRefusedAsTheirPathway()
    {
        var pathway = GivenRelay(finishedStages: 1);
        var boundary = new Mock<MedApp.Application.Common.Teams.ITeamBoundary>();
        boundary
            .Setup(b => b.EnsureCanChangePathwayAsync(pathway, It.IsAny<MedApp.Application.Common.Teams.TeamScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException(MedApp.Application.Common.Teams.TeamWalls.NotYourTeams("Dermatology")));
        Fixture.Inject(boundary.Object);

        var act = () => Handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(3).Id, In(Guid.NewGuid())), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This pathway is*");
    }
}
