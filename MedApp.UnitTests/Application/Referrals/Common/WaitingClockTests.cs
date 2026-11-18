using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Common;

/// <summary>
/// The waiting-time clock's arithmetic (<see cref="WaitingClock"/>) and its changes (<see cref="ReferralChange"/>):
/// whole UK days, pauses that start and end on the day they're recorded, breaches that a pause doesn't undo, and a
/// stop that's for good. A referral here is received on 10 November with High's target (28 days): its target date is
/// 8 December.
/// </summary>
[TestFixture]
public sealed class WaitingClockTests
{
    private static readonly DateOnly TargetOn = ReceivedOn.AddDays(28);

    private static DateTime At(DateOnly ukDate) => DueDates.StartOfDayUtc(ukDate).AddHours(10);

    private static Referral Accepted()
    {
        var referral = CreateReferral(status: ReferralStatus.Accepted);
        referral.PathwayId = Guid.NewGuid();
        return referral;
    }

    [Test]
    public void Start_CountsFromTheDayReceivedWithItsTarget()
    {
        var referral = CreateReferral();

        referral.TargetDays.Should().Be(28);
        referral.WaitStartOn.Should().Be(ReceivedOn);
        referral.TargetOn.Should().Be(TargetOn);
        referral.PausedDays.Should().Be(0);
        WaitingClock.State(referral).Should().Be(WaitingClock.Running);
    }

    [TestCase(PatientPathwayPriority.Urgent, 14)]
    [TestCase(PatientPathwayPriority.High, 28)]
    [TestCase(PatientPathwayPriority.Medium, 126)]
    [TestCase(PatientPathwayPriority.Low, 126)]
    public void TargetDaysFor_TheDefaults(PatientPathwayPriority priority, int days) =>
        WaitingClock.TargetDaysFor(new Team(), priority).Should().Be(days);

    [Test]
    public void TargetDaysFor_ReadsTheTeamsOwnTable()
    {
        var team = new Team { TargetDaysUrgent = 7, TargetDaysHigh = 21, TargetDaysMedium = 84, TargetDaysLow = 180 };

        WaitingClock.TargetDaysFor(team, PatientPathwayPriority.Urgent).Should().Be(7);
        WaitingClock.TargetDaysFor(team, PatientPathwayPriority.High).Should().Be(21);
        WaitingClock.TargetDaysFor(team, PatientPathwayPriority.Medium).Should().Be(84);
        WaitingClock.TargetDaysFor(team, PatientPathwayPriority.Low).Should().Be(180);
    }

    [Test]
    public void Running_CountsToTodayAndBreachesOnlyAfterTheTargetDate()
    {
        var referral = CreateReferral();

        WaitingClock.DaysWaited(referral, Today).Should().Be(8);
        WaitingClock.IsBreached(referral, TargetOn).Should().BeFalse("on the target date itself it's still within it");
        WaitingClock.IsBreached(referral, TargetOn.AddDays(1)).Should().BeTrue();
        WaitingClock.DaysWaited(referral, TargetOn.AddDays(1)).Should().Be(29);
        WaitingClock.PathwayTargetOn(referral).Should().Be(TargetOn);
    }

    [Test]
    public void DaysWaited_NeverBelowZero()
    {
        var referral = CreateReferral();

        WaitingClock.DaysWaited(referral, ReceivedOn.AddDays(-3)).Should().Be(0);
    }

    [Test]
    public void Pause_FreezesTheWaitAndTheReviewDateBecomesThePathwaysTarget()
    {
        var referral = Accepted();
        var pausedOn = new DateOnly(2026, 11, 20);
        var change = new ReferralChange(referral, Registrar, At(pausedOn));

        var pause = change.PauseClock(ClockPauseReason.PatientChoseToWait, null, pausedOn.AddDays(30));

        pause.PausedOn.Should().Be(pausedOn);
        pause.ReviewOn.Should().Be(pausedOn.AddDays(30));
        referral.Pauses.Should().ContainSingle().Which.Should().Be(pause);
        WaitingClock.State(referral).Should().Be(WaitingClock.Paused);
        WaitingClock.DaysWaited(referral, pausedOn.AddDays(20)).Should().Be(10, "the days paused don't count");
        WaitingClock.PathwayTargetOn(referral).Should().Be(pausedOn.AddDays(30));
        WaitingClock.IsReviewOverdue(referral, pausedOn.AddDays(30)).Should().BeFalse();
        WaitingClock.IsReviewOverdue(referral, pausedOn.AddDays(31)).Should().BeTrue();
        WaitingClock.OpenPause(referral).Should().Be(pause);

        var entry = change.Events.Single();
        entry.Action.Should().Be(ReferralEventAction.ClockPaused);
        var details = ReferralEventDetails.Parse(entry.Details);
        details.PauseReason.Should().Be("PatientChoseToWait");
        details.ReviewOn.Should().Be(pausedOn.AddDays(30));
        details.PauseId.Should().Be(pause.Id);
    }

    [Test]
    public void Pause_TheDetailStaysOnThePauseNeverInTheHistory()
    {
        var referral = Accepted();
        var change = new ReferralChange(referral, Registrar, NowUtc);

        var pause = change.PauseClock(ClockPauseReason.Other, Canary, Today.AddDays(5));

        pause.Detail.Should().Be(Canary);
        change.Events.Should().AllSatisfy(e => e.Details.Should().NotContain(Canary));
    }

    [Test]
    public void Resume_AddsThePausedDaysSoTheTargetMovesOnAsFar()
    {
        var referral = Accepted();
        var pausedOn = new DateOnly(2026, 11, 20);
        var pause = new ReferralChange(referral, Registrar, At(pausedOn))
            .PauseClock(ClockPauseReason.PatientUnavailable, null, pausedOn.AddDays(30));
        var resumedOn = pausedOn.AddDays(10);
        var change = new ReferralChange(referral, Registrar, At(resumedOn));

        change.ResumeClock(pause);

        pause.ResumedOn.Should().Be(resumedOn);
        pause.ResumedByUsername.Should().Be(Registrar.Username);
        pause.Days.Should().Be(10);
        referral.PausedDays.Should().Be(10);
        referral.WaitStartOn.Should().Be(ReceivedOn.AddDays(10));
        referral.TargetOn.Should().Be(TargetOn.AddDays(10));
        referral.PausedSinceOn.Should().BeNull();
        referral.PauseReviewOn.Should().BeNull();
        WaitingClock.State(referral).Should().Be(WaitingClock.Running);
        WaitingClock.DaysWaited(referral, resumedOn).Should().Be(10, "10 days before the pause, none during it");
        WaitingClock.PathwayTargetOn(referral).Should().Be(TargetOn.AddDays(10));

        var details = ReferralEventDetails.Parse(change.Events.Single().Details);
        details.PauseReason.Should().Be("PatientUnavailable");
        details.Automatic.Should().BeFalse();
    }

    [Test]
    public void PauseStartedAndEndedTheSameDay_CountsNothing()
    {
        var referral = Accepted();
        var change = new ReferralChange(referral, Registrar, NowUtc);

        var pause = change.PauseClock(ClockPauseReason.PatientChoseToWait, null, Today.AddDays(1));
        change.ResumeClock(pause);

        pause.Days.Should().Be(0);
        referral.PausedDays.Should().Be(0);
        referral.TargetOn.Should().Be(TargetOn);
    }

    [Test]
    public void BreachedBeforeAPause_StaysBreachedWhilePausedAndAfter()
    {
        var referral = Accepted();
        var pausedOn = TargetOn.AddDays(5);
        var pause = new ReferralChange(referral, Registrar, At(pausedOn))
            .PauseClock(ClockPauseReason.PatientChoseToWait, null, pausedOn.AddDays(60));

        WaitingClock.IsBreached(referral, pausedOn.AddDays(50)).Should().BeTrue();

        new ReferralChange(referral, Registrar, At(pausedOn.AddDays(50))).ResumeClock(pause);

        WaitingClock.IsBreached(referral, pausedOn.AddDays(50)).Should().BeTrue();
        WaitingClock.DaysWaited(referral, pausedOn.AddDays(50)).Should().Be(33);
    }

    [Test]
    public void ChangeTime_CountsInUkDates()
    {
        // 23:30 UTC on Saturday 28 March 2026 is 23:30 GMT, still the 28th in the UK; an hour later the clocks have
        // gone forward and it's 01:30 BST on the 29th.
        new ReferralChange(CreateReferral(), Registrar, new DateTime(2026, 3, 28, 23, 30, 0, DateTimeKind.Utc))
            .Today.Should().Be(new DateOnly(2026, 3, 28));
        new ReferralChange(CreateReferral(), Registrar, new DateTime(2026, 3, 29, 0, 30, 0, DateTimeKind.Utc))
            .Today.Should().Be(new DateOnly(2026, 3, 29));
        // In summer, 23:30 UTC is already the next day in the UK.
        new ReferralChange(CreateReferral(), Registrar, new DateTime(2026, 6, 14, 23, 30, 0, DateTimeKind.Utc))
            .Today.Should().Be(new DateOnly(2026, 6, 15));
    }

    [Test]
    public void Stop_WhilePaused_EndsThePauseFirstSoItsDaysDontCount()
    {
        var referral = Accepted();
        var pausedOn = new DateOnly(2026, 11, 20);
        var pause = new ReferralChange(referral, Registrar, At(pausedOn))
            .PauseClock(ClockPauseReason.PatientChoseToWait, null, pausedOn.AddDays(30));
        var stoppedOn = pausedOn.AddDays(7);
        var change = new ReferralChange(referral, Registrar, At(stoppedOn));

        change.StopClock(ClockStopReason.PathwayCancelled);

        pause.ResumedOn.Should().Be(stoppedOn);
        referral.PausedDays.Should().Be(7);
        referral.StoppedOn.Should().Be(stoppedOn);
        referral.StopReason.Should().Be(ClockStopReason.PathwayCancelled);
        WaitingClock.State(referral).Should().Be(WaitingClock.Stopped);
        WaitingClock.DaysWaited(referral, stoppedOn.AddDays(100)).Should().Be(10, "frozen at the stop");
        WaitingClock.PathwayTargetOn(referral).Should().BeNull("a stopped clock leaves the target date to staff");
        change.Events.Select(e => e.Action).Should().Equal(ReferralEventAction.ClockResumed, ReferralEventAction.ClockStopped);
        ReferralEventDetails.Parse(change.Events[0].Details).Automatic.Should().BeTrue();
        var stopped = ReferralEventDetails.Parse(change.Events[1].Details);
        stopped.StopReason.Should().Be("PathwayCancelled");
        stopped.DaysWaited.Should().Be(10);
        stopped.TargetDays.Should().Be(28);
        stopped.Breached.Should().BeFalse();
    }

    [Test]
    public void Stop_IsForGood()
    {
        var referral = Accepted();
        var change = new ReferralChange(referral, Registrar, NowUtc);
        change.StopClock(ClockStopReason.StopStageFinished);

        change.StopClock(ClockStopReason.PathwayDischarged);
        change.ReTarget(referral.Team, PatientPathwayPriority.Urgent).Should().BeFalse();

        change.Events.Should().ContainSingle();
        referral.StopReason.Should().Be(ClockStopReason.StopStageFinished);
        referral.TargetDays.Should().Be(28);
        WaitingClock.WhyCantPause(referral).Should().Be(WaitingClock.StoppedMessage);
        WaitingClock.WhyCantResume(referral).Should().Be(WaitingClock.StoppedMessage);
        WaitingClock.SetsPathwayTarget(referral).Should().BeFalse();
    }

    [Test]
    public void ReTarget_RecordsTheOldAndNewDaysOnlyWhenTheyDiffer()
    {
        var referral = Accepted();
        var change = new ReferralChange(referral, Registrar, NowUtc);

        change.ReTarget(referral.Team, PatientPathwayPriority.High).Should().BeFalse("High is 28 days already");
        change.ReTarget(referral.Team, PatientPathwayPriority.Low).Should().BeTrue();

        referral.TargetDays.Should().Be(126);
        referral.TargetOn.Should().Be(ReceivedOn.AddDays(126));
        var details = ReferralEventDetails.Parse(change.Events.Single().Details);
        details.PreviousTargetDays.Should().Be(28);
        details.TargetDays.Should().Be(126);
    }

    [Test]
    public void RedirectToAnotherTeam_ReReadsTheTargetFromItsTableFromTheSameDay()
    {
        var referral = CreateReferral();
        var urology = new Team { Id = Guid.NewGuid(), Name = TeamsTestConstants.OtherTeamName, TargetDaysHigh = 42 };
        var change = new ReferralChange(referral, Triager, NowUtc);

        change.RedirectToTeam(urology, Detail);

        referral.TargetDays.Should().Be(42);
        referral.WaitStartOn.Should().Be(ReceivedOn);
        change.Events.Select(e => e.Action).Should().Equal(ReferralEventAction.TeamChanged, ReferralEventAction.TargetChanged);
        WaitingClock.State(referral).Should().Be(WaitingClock.Running);
    }

    [Test]
    public void Register_StartsTheClockWithTheTargetForThePriorityAskedFor()
    {
        var referral = CreateReferral(version: 0);
        referral.RequestedPriority = PatientPathwayPriority.Urgent;
        var change = new ReferralChange(referral, Registrar, NowUtc);

        change.Register(referral.Team);

        referral.TargetDays.Should().Be(14);
        referral.TargetOn.Should().Be(ReceivedOn.AddDays(14));
        ReferralEventDetails.Parse(change.Events.Single().Details).TargetDays.Should().Be(14);
    }

    [Test]
    public void EditDetails_ACorrectedDateReceived_MovesTheDatesItCountsFrom()
    {
        var referral = CreateReferral();
        var change = new ReferralChange(referral, Registrar, NowUtc);

        change.EditDetails(
            new ReferralDetails(referral.TeamId, ReceivedOn.AddDays(-4), Practice, Clinician, OdsCode, PatientPathwayPriority.High, Reason),
            referral.Team, referral.Team);

        referral.WaitStartOn.Should().Be(ReceivedOn.AddDays(-4));
        referral.TargetOn.Should().Be(TargetOn.AddDays(-4));
        change.Events.Should().ContainSingle("the target in days is unchanged");
    }

    [Test]
    public void WhyCantPause_OnlyAnAcceptedReferralsRunningClock()
    {
        WaitingClock.WhyCantPause(CreateReferral()).Should().Be(WaitingClock.NotAcceptedMessage);

        var referral = Accepted();
        WaitingClock.WhyCantPause(referral).Should().BeNull();
        WaitingClock.WhyCantResume(referral).Should().Be(WaitingClock.NotPausedMessage);

        new ReferralChange(referral, Registrar, NowUtc).PauseClock(ClockPauseReason.Other, Detail, Today.AddDays(3));
        WaitingClock.WhyCantPause(referral).Should().Be(WaitingClock.AlreadyPausedMessage);
        WaitingClock.WhyCantResume(referral).Should().BeNull();
    }

    [TestCase(ReferralStatus.Rejected, ClockStopReason.Rejected)]
    [TestCase(ReferralStatus.RedirectedElsewhere, ClockStopReason.RedirectedElsewhere)]
    [TestCase(ReferralStatus.Withdrawn, ClockStopReason.Withdrawn)]
    [TestCase(ReferralStatus.EnteredInError, ClockStopReason.EnteredInError)]
    public void StopReasonFor_AReferralClosedWithoutAPathway(ReferralStatus status, ClockStopReason reason) =>
        WaitingClock.StopReasonFor(status).Should().Be(reason);

    [TestCase(ReferralStatus.AwaitingTriage)]
    [TestCase(ReferralStatus.Accepted)]
    public void StopReasonFor_OpenOrAccepted_IsNone(ReferralStatus status) =>
        WaitingClock.StopReasonFor(status).Should().BeNull();

    [Test]
    public void SetsPathwayTarget_NoReferral_IsFalse() => WaitingClock.SetsPathwayTarget(null).Should().BeFalse();

    [Test]
    public void LatestReviewOn_Is90DaysAhead() =>
        WaitingClock.LatestReviewOn(Today).Should().Be(Today.AddDays(ReferralClockPause.MaxReviewDays));
}
