using FluentAssertions;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Queries.SearchReferrals;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Common;

[TestFixture]
public sealed class ReferralMapperTests
{
    [Test]
    public void ToResponse_GivesTheBannerDetailsHistoryAndWhatTheRegistrarMayDo()
    {
        var referral = CreateReferral(version: 2);
        var change = new ReferralChange(referral, Registrar, NowUtc);
        change.Register(referral.Team);

        var response = ReferralMapper.ToResponse(referral, change.Events, letterCount: 1, Registrar);

        response.Patient!.Id.Should().Be(referral.PatientId);
        response.PatientFlags.Should().NotBeNull();
        response.TeamName.Should().Be(TeamsTestConstants.TeamName);
        response.Reason.Should().Be(Reason);
        response.ReferrerPractice.Should().Be(Practice);
        response.RequestedPriority.Should().Be("High");
        response.Status.Should().Be("AwaitingTriage");
        response.IsOpen.Should().BeTrue();
        response.LetterCount.Should().Be(1);
        response.CanEdit.Should().BeTrue();
        response.CanClose.Should().BeTrue();
        response.CanAttachFiles.Should().BeTrue();
        response.ReadOnlyReason.Should().BeNull();
        response.CanTriage.Should().BeFalse();
        response.WhyCantTriage.Should().Be(ReferralWorkflow.NoTriagePermissionMessage);
        response.History.Single().Action.Should().Be("Registered");
        response.History.Single().TeamName.Should().Be(TeamsTestConstants.TeamName);
        response.History.Single().RequestedPriority.Should().Be("High");
    }

    [Test]
    public void ToResponse_AViewerWithoutRegisterReferrals_CanDoNothing()
    {
        var response = ReferralMapper.ToResponse(CreateReferral(), [], 0, Viewer);

        response.CanEdit.Should().BeFalse();
        response.CanClose.Should().BeFalse();
        response.CanAttachFiles.Should().BeFalse();
    }

    [Test]
    public void ToResponse_RestrictedRecord_CanBeWithdrawnButNotEditedOrGivenFiles()
    {
        var referral = CreateReferral();
        referral.Patient.RestrictedAtUtc = NowUtc;

        var response = ReferralMapper.ToResponse(referral, [], 0, Registrar);

        response.CanEdit.Should().BeFalse();
        response.CanClose.Should().BeTrue();
        response.CanAttachFiles.Should().BeFalse();
        response.ReadOnlyReason.Should().NotBeNull();
        response.PatientFlags!.IsRestricted.Should().BeTrue();
    }

    [Test]
    public void ToResponse_Closed_SaysWhyAndOffersNoTriage()
    {
        var referral = CreateReferral(status: ReferralStatus.Withdrawn);
        referral.DecisionDetail = Detail;
        referral.DecidedByUsername = Registrar.Username;

        var response = ReferralMapper.ToResponse(referral, [], 0, Registrar);

        response.IsOpen.Should().BeFalse();
        response.CanEdit.Should().BeFalse();
        response.CanClose.Should().BeFalse();
        response.ReadOnlyReason.Should().Contain("withdrawn");
        response.WhyCantTriage.Should().BeNull();
        response.DecisionDetail.Should().Be(Detail);
        response.DecidedByUsername.Should().Be(Registrar.Username);
    }

    [Test]
    public void ToResponse_ATriagerInItsTeam_CanTriage()
    {
        var response = ReferralMapper.ToResponse(CreateReferral(), [], 0, Triager, viewerIsMember: true);

        response.CanTriage.Should().BeTrue();
        response.WhyCantTriage.Should().BeNull();
    }

    [Test]
    public void ToResponse_ATriagerOutsideItsTeam_IsToldWhoCan()
    {
        var response = ReferralMapper.ToResponse(CreateReferral(), [], 0, Triager, viewerIsMember: false);

        response.CanTriage.Should().BeFalse();
        response.WhyCantTriage.Should().Be(ReferralWorkflow.NotInTeamMessage(TeamsTestConstants.TeamName));
    }

    [Test]
    public void ToResponse_ATriagerOnARestrictedRecord_CantTriage()
    {
        var referral = CreateReferral();
        referral.Patient.RestrictedAtUtc = NowUtc;

        var response = ReferralMapper.ToResponse(referral, [], 0, Triager, viewerIsMember: true);

        response.CanTriage.Should().BeFalse();
        response.WhyCantTriage.Should().NotBeNull();
    }

    [Test]
    public void ToResponse_Triaged_GivesTheDecisionAndHistoryWithoutFreeTextInTheHistory()
    {
        var referral = CreateReferral();
        var change = new ReferralChange(referral, Triager, NowUtc);
        change.Reject(ReferralRejectionReason.Duplicate, Canary);

        var response = ReferralMapper.ToResponse(referral, change.Events, 0, Triager, viewerIsMember: true);

        response.Status.Should().Be("Rejected");
        response.RejectionReason.Should().Be("Duplicate");
        response.DecisionDetail.Should().Be(Canary);
        response.DecidedByUsername.Should().Be(Triager.Username);
        response.CanTriage.Should().BeFalse();
        response.WhyCantTriage.Should().BeNull();
        response.History.Select(e => e.Action).Should().Equal("Rejected", "ClockStopped");
        var entry = response.History[0];
        entry.RejectionReason.Should().Be("Duplicate");
        entry.ToStatus.Should().Be("Rejected");
        var stopped = response.History[1];
        stopped.StopReason.Should().Be("Rejected");
        stopped.DaysWaited.Should().Be(8);
        stopped.TargetDays.Should().Be(28);
        stopped.Breached.Should().BeFalse();
        change.Events.Should().AllSatisfy(e => e.Details.Should().NotContain(Canary));
    }

    [Test]
    public void ToResponse_RedirectedHere_GivesTheRedirectDetail()
    {
        var referral = CreateReferral();
        referral.RedirectDetail = Detail;

        ReferralMapper.ToResponse(referral, [], 0, Viewer).RedirectDetail.Should().Be(Detail);
    }

    [Test]
    public void ToPathwayReferral_GivesItsIdDateAndTeam()
    {
        var referral = CreateReferral();

        var response = ReferralMapper.ToPathwayReferral(referral, Viewer, Today);

        response.Id.Should().Be(referral.Id);
        response.ReceivedOn.Should().Be(ReceivedOn);
        response.TeamName.Should().Be(TeamsTestConstants.TeamName);
        response.Version.Should().Be(referral.Version);
        response.Clock!.DaysWaited.Should().Be(8);
    }

    [Test]
    public void EventDetails_Accepted_GivesThePriorityTemplateAndPathway()
    {
        var referral = CreateReferral();
        var pathwayId = Guid.NewGuid();
        var change = new ReferralChange(referral, Triager, NowUtc);
        change.Accept(pathwayId, "Synthetic template", MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority.Urgent);

        var entry = ReferralMapper.ToResponse(change.Events.Single());

        entry.Action.Should().Be("Accepted");
        entry.Priority.Should().Be("Urgent");
        entry.TemplateName.Should().Be("Synthetic template");
        entry.PathwayId.Should().Be(pathwayId);
        entry.TeamName.Should().Be(TeamsTestConstants.TeamName);
    }

    [Test]
    public void EventDetails_TeamChanged_GivesBothTeams()
    {
        var referral = CreateReferral();
        var urology = TeamsTestConstants.CreateTeam(TeamsTestConstants.OtherTeamName);
        var change = new ReferralChange(referral, Triager, NowUtc);
        change.RedirectToTeam(urology, Canary);

        var entry = ReferralMapper.ToResponse(change.Events.Single());

        entry.Action.Should().Be("TeamChanged");
        entry.TeamName.Should().Be(TeamsTestConstants.OtherTeamName);
        entry.PreviousTeamName.Should().Be(TeamsTestConstants.TeamName);
        change.Events.Single().Details.Should().NotContain(Canary);
    }

    [Test]
    public void ToSummary_CountsDaysToTodayWhileOpenAndToTheDecisionOnceClosed()
    {
        var open = CreateReferral();
        ReferralMapper.ToSummary(new ReferralRow(open, true), Today).DaysSinceReceived.Should().Be(8);

        var closed = CreateReferral(status: ReferralStatus.Withdrawn);
        closed.DecidedAtUtc = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
        var summary = ReferralMapper.ToSummary(new ReferralRow(closed, false), Today);

        summary.DaysSinceReceived.Should().Be(2);
        summary.HasLetter.Should().BeFalse();
        summary.PatientName.Should().Be($"{closed.Patient.FirstName} {closed.Patient.LastName}");
        summary.TeamName.Should().Be(TeamsTestConstants.TeamName);
        summary.ReferrerPractice.Should().Be(Practice);
        summary.Status.Should().Be("Withdrawn");
    }

    [Test]
    public void ToClock_GivesTheWaitTheTargetAndThePause()
    {
        var referral = CreateReferral(status: ReferralStatus.Accepted);
        var running = ReferralMapper.ToClock(referral, Today);

        running.Should().BeEquivalentTo(new
        {
            State = WaitingClock.Running, TargetDays = 28, WaitStartOn = ReceivedOn, TargetOn = ReceivedOn.AddDays(28),
            DaysWaited = 8, PausedDays = 0, IsBreached = false, PausedSinceOn = (DateOnly?)null, PauseReviewOn = (DateOnly?)null,
            IsReviewOverdue = false, StoppedOn = (DateOnly?)null, StopReason = (string?)null
        });

        new ReferralChange(referral, Registrar, NowUtc).PauseClock(ClockPauseReason.PatientUnavailable, Detail, Today.AddDays(3));
        var paused = ReferralMapper.ToClock(referral, Today.AddDays(40));
        paused.State.Should().Be(WaitingClock.Paused);
        paused.DaysWaited.Should().Be(8);
        paused.PausedSinceOn.Should().Be(Today);
        paused.PauseReviewOn.Should().Be(Today.AddDays(3));
        paused.IsReviewOverdue.Should().BeTrue();

        var pauses = ReferralMapper.ToPauses(referral);
        pauses.Single().Should().BeEquivalentTo(new
        {
            PausedOn = Today, PausedByUsername = Registrar.Username, Reason = "PatientUnavailable", Detail, ReviewOn = Today.AddDays(3),
            ResumedOn = (DateOnly?)null, Days = 0
        });
    }

    [Test]
    public void ToResponse_PauseAndResumeFlags_FollowThePermissionTheClockAndTheRecord()
    {
        var worker = new MedApp.Application.Pathways.Common.PathwayActor(Guid.NewGuid(), "w.synthetic", TestPermissions.Staff);
        var referral = CreateReferral(status: ReferralStatus.Accepted);

        ReferralMapper.ToResponse(referral, [], 0, worker, today: Today).Should().BeEquivalentTo(new { CanPauseClock = true, CanResumeClock = false });
        ReferralMapper.ToResponse(referral, [], 0, Viewer, today: Today).CanPauseClock.Should().BeFalse("viewing isn't working on pathways");
        ReferralMapper.ToResponse(CreateReferral(), [], 0, worker, today: Today).CanPauseClock.Should().BeFalse("before triage it always runs");

        new ReferralChange(referral, worker, NowUtc).PauseClock(ClockPauseReason.Other, Detail, Today.AddDays(3));
        ReferralMapper.ToResponse(referral, [], 0, worker, today: Today).Should().BeEquivalentTo(new { CanPauseClock = false, CanResumeClock = true });

        referral.Patient.RestrictedAtUtc = NowUtc;
        ReferralMapper.ToResponse(referral, [], 0, worker, today: Today).CanResumeClock.Should().BeFalse("a restricted record can't change");
    }

    [Test]
    public void ToSummary_GivesTheClock()
    {
        var referral = CreateReferral();

        ReferralMapper.ToSummary(new ReferralRow(referral, true), ReceivedOn.AddDays(30)).Clock!
            .Should().BeEquivalentTo(new { DaysWaited = 30, IsBreached = true, TargetDays = 28 });
    }

    [Test]
    public void EventDetails_NotJson_IsEmpty()
    {
        var parsed = ReferralEventDetails.Parse("not json");

        parsed.ChangedFields.Should().BeEmpty();
        parsed.ToStatus.Should().BeNull();
    }
}

[TestFixture]
public sealed class ReferralSearchTests
{
    private static SearchReferralsQuery Query(
        IReadOnlyList<string>? statuses = null, Guid? patientId = null, string? search = null, string? sort = null, bool ascending = true) =>
        new(1, 10, sort, ascending, statuses, null, false, patientId, search, Registrar.UserId);

    [Test]
    public void Filter_ByStatusPatientTeamAndName()
    {
        var awaiting = CreateReferral();
        var withdrawn = CreateReferral(status: ReferralStatus.Withdrawn);
        var other = CreateReferral(team: TeamsTestConstants.CreateTeam(TeamsTestConstants.OtherTeamName));
        other.Patient.FirstName = "Zelda";
        var all = new[] { awaiting, withdrawn, other }.AsQueryable();

        all.Filter(Query(["AwaitingTriage"]), null).Should().BeEquivalentTo([awaiting, other]);
        all.Filter(Query(patientId: withdrawn.PatientId), null).Should().Equal(withdrawn);
        all.Filter(Query(), [other.TeamId]).Should().Equal(other);
        all.Filter(Query(search: "zeld"), null).Should().Equal(other);
    }

    [Test]
    public void Filter_ByTheClock_BreachedPausedOrStopped()
    {
        var within = CreateReferral();
        var breached = CreateReferral();
        breached.ReceivedOn = Today.AddDays(-40);
        MedApp.Application.Referrals.Common.WaitingClock.Recalculate(breached);
        var paused = CreateReferral(status: ReferralStatus.Accepted);
        paused.PausedSinceOn = Today;
        paused.PauseReviewOn = Today.AddDays(5);
        var stopped = CreateReferral(status: ReferralStatus.Rejected);
        stopped.StoppedOn = Today;
        var all = new[] { within, breached, paused, stopped }.AsQueryable();

        all.Filter(Query() with { BreachedOnly = true }, null, Today).Should().Equal(breached);
        all.Filter(Query() with { Paused = true }, null, Today).Should().Equal(paused);
        all.Filter(Query() with { Paused = false }, null, Today).Should().BeEquivalentTo([within, breached, stopped]);
        all.Filter(Query() with { ClockStopped = true }, null, Today).Should().Equal(stopped);
        all.Filter(Query() with { ClockStopped = false }, null, Today).Should().BeEquivalentTo([within, breached, paused]);
        ReferralSearch.FiltersUsed(Query() with { BreachedOnly = true, Paused = true, ClockStopped = false })
            .Should().Equal("breachedOnly", "paused", "clockStopped");
    }

    [Test]
    public void Sort_DaysWaitedTargetAndReviewDate()
    {
        var shorter = CreateReferral();
        var longer = CreateReferral();
        longer.ReceivedOn = Today.AddDays(-20);
        MedApp.Application.Referrals.Common.WaitingClock.Recalculate(longer);
        var reviewSoon = CreateReferral(status: ReferralStatus.Accepted);
        reviewSoon.ReceivedOn = Today.AddDays(-3);
        MedApp.Application.Referrals.Common.WaitingClock.Recalculate(reviewSoon);
        reviewSoon.PausedSinceOn = Today;
        reviewSoon.PauseReviewOn = Today.AddDays(2);
        var all = new[] { shorter, longer, reviewSoon }.AsQueryable();

        all.Sort("daysWaited", false, Today).Should().Equal(longer, shorter, reviewSoon);
        all.Sort("daysWaited", true, Today).Should().Equal(reviewSoon, shorter, longer);
        all.Sort("targetOn", true, Today).Should().Equal(longer, shorter, reviewSoon);
        all.Sort("targetOn", false, Today).Should().Equal(reviewSoon, shorter, longer);
        all.Sort("reviewOn", true, Today).First().Should().Be(reviewSoon);
        all.Sort("reviewOn", false, Today).First().Should().Be(reviewSoon);
    }

    [Test]
    public void Sort_OldestReceivedFirstByDefault_ThenById()
    {
        var newer = CreateReferral();
        var older = CreateReferral();
        older.ReceivedOn = ReceivedOn.AddDays(-5);
        var all = new[] { newer, older }.AsQueryable();

        all.Sort(null, true).Should().Equal(older, newer);
        all.Sort("receivedOn", false).Should().Equal(newer, older);
        all.Sort("priority", true).Should().HaveCount(2);
    }

    [TestCase("receivedOn", true)]
    [TestCase("PATIENT", true)]
    [TestCase("team", true)]
    [TestCase("priority", true)]
    [TestCase("registered", true)]
    [TestCase("daysWaited", true)]
    [TestCase("targetOn", true)]
    [TestCase("reviewOn", true)]
    [TestCase("waitedLongest", false)]
    public void IsSortColumn(string sort, bool valid) => ReferralSearch.IsSortColumn(sort).Should().Be(valid);
}
