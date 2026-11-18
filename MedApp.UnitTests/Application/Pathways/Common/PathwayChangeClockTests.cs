using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.Common;

/// <summary>
/// A pathway accepted from a referral, and its waiting-time clock (<see cref="PathwayChange"/>): the clock-stop stage,
/// discharge and cancellation stop it; On hold doesn't; a new priority re-reads its target; and the pathway's target
/// date follows it, recorded as a target date change from the clock.
/// </summary>
[TestFixture]
public sealed class PathwayChangeClockTests
{
    /// <summary>A pathway in progress accepted from a referral received at <see cref="Today"/> minus 10 days, High (28 days).</summary>
    private static (PatientPathway Pathway, Referral Referral) AcceptedPathway(
        PatientPathwayStatus status = PatientPathwayStatus.InProgress, int finishedStages = 0)
    {
        var pathway = CreatePathway(status, finishedStages, priority: PatientPathwayPriority.High);
        var referral = ReferralsTestConstants.CreateReferral(patient: pathway.Patient, status: ReferralStatus.Accepted);
        referral.ReceivedOn = Today.AddDays(-10);
        WaitingClock.Start(referral, 28);
        referral.PathwayId = pathway.Id;
        pathway.Referral = referral;
        pathway.DueDateUtc = DueDates.EndOfDayUtc(referral.TargetOn);
        return (pathway, referral);
    }

    [Test]
    public void FinishStage_TheClockStopStage_StopsTheClock()
    {
        var (pathway, referral) = AcceptedPathway();
        var stage = pathway.Stages[0];
        stage.StopsClock = true;
        var change = new PathwayChange(pathway, Staff, Now);

        change.FinishStage(stage);

        referral.StoppedOn.Should().Be(Today);
        referral.StopReason.Should().Be(ClockStopReason.StopStageFinished);
        change.ReferralEvents.Single().Action.Should().Be(ReferralEventAction.ClockStopped);
        change.HasChanges.Should().BeTrue();
    }

    [Test]
    public void FinishStage_AnotherStage_LeavesTheClockRunning()
    {
        var (pathway, referral) = AcceptedPathway();
        pathway.Stages[1].StopsClock = true;
        var change = new PathwayChange(pathway, Staff, Now);

        change.FinishStage(pathway.Stages[0]);

        WaitingClock.State(referral).Should().Be(WaitingClock.Running);
        change.ReferralEvents.Should().BeEmpty();
    }

    [Test]
    public void FinishStage_AgainAfterAnUnlock_NeverRestartsOrStopsItAgain()
    {
        var (pathway, referral) = AcceptedPathway();
        var stage = pathway.Stages[0];
        stage.StopsClock = true;
        new PathwayChange(pathway, Staff, Now).FinishStage(stage);

        var later = new PathwayChange(pathway, Staff, Now.AddDays(3));
        later.UnlockStage(stage);
        later.FinishStage(stage);

        referral.StoppedOn.Should().Be(Today);
        later.ReferralEvents.Should().BeEmpty();
    }

    [TestCase(PatientPathwayStatus.Completed, ClockStopReason.PathwayDischarged)]
    [TestCase(PatientPathwayStatus.Cancelled, ClockStopReason.PathwayCancelled)]
    public void ChangeStatus_DischargedOrCancelled_StopsTheClock(PatientPathwayStatus to, ClockStopReason reason)
    {
        var (pathway, referral) = AcceptedPathway(PatientPathwayStatus.InReview, finishedStages: 3);
        var change = new PathwayChange(pathway, Staff, Now);

        change.ChangeStatus(to, to == PatientPathwayStatus.Cancelled ? "Synthetic reason" : null);

        referral.StopReason.Should().Be(reason);
        change.ReferralEvents.Single().Action.Should().Be(ReferralEventAction.ClockStopped);
    }

    [Test]
    public void ChangeStatus_OnHold_DoesntPauseTheClock()
    {
        var (pathway, referral) = AcceptedPathway();
        var change = new PathwayChange(pathway, Staff, Now);

        change.ChangeStatus(PatientPathwayStatus.Blocked, "Waiting for histology");

        WaitingClock.State(referral).Should().Be(WaitingClock.Running);
        change.ReferralEvents.Should().BeEmpty();
    }

    [Test]
    public void PriorityChanged_Running_ReReadsTheTargetAndMovesTheTargetDateAsTheClocksChange()
    {
        var (pathway, referral) = AcceptedPathway();
        var before = DueDates.ToUkDate(pathway.DueDateUtc);
        var change = new PathwayChange(pathway, Staff, Now);
        pathway.Priority = PatientPathwayPriority.Urgent;

        change.PriorityChanged();

        referral.TargetDays.Should().Be(14);
        DueDates.ToUkDate(pathway.DueDateUtc).Should().Be(Today.AddDays(4));
        change.ReferralEvents.Single().Action.Should().Be(ReferralEventAction.TargetChanged);
        var entry = change.AuditEntries.Single();
        entry.Action.Should().Be(PatientPathwayAuditAction.DetailsUpdated);
        entry.Details.Should().Contain("\"source\":\"clock\"")
            .And.Contain($"\"dueDate\":{{\"from\":\"{before:yyyy-MM-dd}\",\"to\":\"{Today.AddDays(4):yyyy-MM-dd}\"}}");
    }

    [Test]
    public void PriorityChanged_Paused_ReReadsTheTargetButTheTargetDateStaysTheReviewDate()
    {
        var (pathway, referral) = AcceptedPathway();
        var review = Today.AddDays(20);
        new PathwayChange(pathway, Staff, Now).PauseClock(ClockPauseReason.PatientChoseToWait, null, review);
        var change = new PathwayChange(pathway, Staff, Now.AddDays(1));
        pathway.Priority = PatientPathwayPriority.Low;

        change.PriorityChanged();

        referral.TargetDays.Should().Be(126);
        DueDates.ToUkDate(pathway.DueDateUtc).Should().Be(review);
        change.AuditEntries.Should().BeEmpty();
    }

    [Test]
    public void PriorityChanged_SameTargetOrStoppedOrNoReferral_ChangesNothing()
    {
        var (sameTarget, _) = AcceptedPathway();
        var same = new PathwayChange(sameTarget, Staff, Now);
        same.PriorityChanged();
        same.HasChanges.Should().BeFalse();

        var (stoppedPathway, stopped) = AcceptedPathway();
        stopped.StoppedOn = Today;
        stoppedPathway.Priority = PatientPathwayPriority.Urgent;
        var afterStop = new PathwayChange(stoppedPathway, Staff, Now);
        afterStop.PriorityChanged();
        afterStop.FollowClock();
        afterStop.HasChanges.Should().BeFalse();

        var direct = CreatePathway(priority: PatientPathwayPriority.Urgent);
        var noClock = new PathwayChange(direct, Staff, Now);
        noClock.PriorityChanged();
        noClock.FollowClock();
        noClock.HasChanges.Should().BeFalse();
        noClock.ReferralEvents.Should().BeEmpty();
    }

    [Test]
    public void PauseClock_TheTargetDateBecomesTheReviewDateAndThePauseIsAdded()
    {
        var (pathway, referral) = AcceptedPathway();
        var review = Today.AddDays(30);
        var change = new PathwayChange(pathway, Staff, Now);

        var pause = change.PauseClock(ClockPauseReason.Other, "Synthetic detail", review);

        change.NewPauses.Should().Equal(pause);
        referral.PauseReviewOn.Should().Be(review);
        DueDates.ToUkDate(pathway.DueDateUtc).Should().Be(review);
        change.ReferralEvents.Single().Action.Should().Be(ReferralEventAction.ClockPaused);
        change.AuditEntries.Single().Details.Should().Contain("\"source\":\"clock\"").And.NotContain("Synthetic detail");
    }

    [Test]
    public void ResumeClock_TheTargetDateGoesBackToTheClocksMovedOnByThePause()
    {
        var (pathway, referral) = AcceptedPathway();
        new PathwayChange(pathway, Staff, Now).PauseClock(ClockPauseReason.PatientUnavailable, null, Today.AddDays(30));
        var change = new PathwayChange(pathway, Staff, Now.AddDays(6));

        change.ResumeClock();

        referral.PausedDays.Should().Be(6);
        DueDates.ToUkDate(pathway.DueDateUtc).Should().Be(Today.AddDays(18 + 6));
        change.ReferralEvents.Single().Action.Should().Be(ReferralEventAction.ClockResumed);
        change.AuditEntries.Single().Action.Should().Be(PatientPathwayAuditAction.DetailsUpdated);
    }

    [Test]
    public void PauseOrResume_WithoutAReferral_IsAProgrammingError()
    {
        var change = new PathwayChange(CreatePathway(), Staff, Now);

        ((Action)(() => change.PauseClock(ClockPauseReason.Other, null, Today.AddDays(1)))).Should().Throw<InvalidOperationException>();
        ((Action)change.ResumeClock).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ResumeClock_NotPaused_IsAProgrammingError()
    {
        var (pathway, _) = AcceptedPathway();

        ((Action)new PathwayChange(pathway, Staff, Now).ResumeClock).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void FromTemplate_CopiesTheClockStopStage()
    {
        var template = new PathwayTemplate
        {
            Id = Guid.NewGuid(),
            Name = TemplateName,
            TeamId = TeamId,
            Maps =
            [
                new PathwayTemplateMap { StageOrder = 1, StageDefinition = new PatientPathwayStageDefinition { Name = "One" } },
                new PathwayTemplateMap { StageOrder = 2, StopsClock = true, StageDefinition = new PatientPathwayStageDefinition { Name = "Two" } }
            ]
        };

        var pathway = PathwayCreation.FromTemplate(template, Guid.NewGuid(), null, Today, PatientPathwayPriority.Low, Now);

        pathway.Stages.Select(s => s.StopsClock).Should().Equal(false, true);
    }

    [Test]
    public void SkipStage_TheClockStopStage_StopsNothingAndDischargeStopsIt()
    {
        var (pathway, referral) = AcceptedPathway();
        var stage = pathway.Stages[0];
        stage.StopsClock = true;
        stage.IsOptional = true;
        var change = new PathwayChange(pathway, Staff, Now);

        change.SkipStage(stage, StageSkipReason.NotNeeded, null);

        WaitingClock.State(referral).Should().Be(WaitingClock.Running);
        change.ReferralEvents.Should().BeEmpty();

        foreach (var other in pathway.Stages.Skip(1))
            change.FinishStage(other);
        change.ChangeStatus(PatientPathwayStatus.Completed, null);

        referral.StopReason.Should().Be(ClockStopReason.PathwayDischarged);
        change.ReferralEvents.Should().ContainSingle(e => e.Action == ReferralEventAction.ClockStopped);
    }

    [Test]
    public void FinishStage_BranchNotChosenHoldsTheClockStopStage_StopsNothing()
    {
        var (pathway, referral) = AcceptedPathway();
        var branching = CreateBranchingPathway();
        foreach (var stage in branching.Stages)
            stage.PatientPathwayId = pathway.Id;
        pathway.Stages = branching.Stages;
        pathway.StageAt(2).StopsClock = true;
        var change = new PathwayChange(pathway, Staff, Now);

        change.FinishStage(pathway.StageAt(1), branch: TreatmentBranch);

        pathway.StageAt(2).IsSkipped.Should().BeTrue();
        WaitingClock.State(referral).Should().Be(WaitingClock.Running);
        change.ReferralEvents.Should().BeEmpty();
    }

    [Test]
    public void ReinstateStage_AfterTheClockStopped_NeverRestartsIt()
    {
        var (pathway, referral) = AcceptedPathway(PatientPathwayStatus.InReview, finishedStages: 2);
        Skip(pathway.StageAt(3));
        new PathwayChange(pathway, Staff, Now).ChangeStatus(PatientPathwayStatus.Completed, null);
        referral.StopReason.Should().Be(ClockStopReason.PathwayDischarged);

        var later = new PathwayChange(pathway, Admin, Now.AddDays(3));
        later.ReinstateStage(pathway.StageAt(3), "Synthetic: needed after all.");

        pathway.Status.Should().Be(PatientPathwayStatus.InProgress);
        WaitingClock.IsStopped(referral).Should().BeTrue();
        referral.StoppedOn.Should().Be(Today);
        later.ReferralEvents.Should().BeEmpty();
    }

    [Test]
    public void ReinstateStage_TheClockStopStageWhileTheClockRuns_StopsItWhenFinished()
    {
        var (pathway, referral) = AcceptedPathway();
        var stage = pathway.Stages[0];
        stage.StopsClock = true;
        Skip(stage);
        var change = new PathwayChange(pathway, Admin, Now);

        change.ReinstateStage(stage, "Synthetic: needed after all.");
        WaitingClock.State(referral).Should().Be(WaitingClock.Running);
        change.FinishStage(stage);

        referral.StopReason.Should().Be(ClockStopReason.StopStageFinished);
    }
}
