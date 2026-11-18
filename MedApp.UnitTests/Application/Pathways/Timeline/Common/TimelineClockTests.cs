using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Domain.Patients;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Pathways.Timeline.Common;

/// <summary>
/// The waiting-time clock on the patient timeline: its own kind (Waiting time), reasons by name, dates and days for
/// everyone, a pause's detail for the full view only; and a target date moved by the clock flagged as such.
/// </summary>
[TestFixture]
public sealed class TimelineClockTests
{
    /// <summary>An accepted referral's clock events: target re-read, paused (detail: the canary), resumed, paused, stopped.</summary>
    private static (Referral Referral, List<ReferralEvent> Events, ReferralClockPause Pause) ClockEvents()
    {
        var referral = CreateReferral(status: ReferralStatus.Accepted);
        var change = new ReferralChange(referral, Registrar, NowUtc);
        change.ReTarget(referral.Team, MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority.Urgent);
        var pause = change.PauseClock(ClockPauseReason.Other, Canary, Today.AddDays(10));
        change.ResumeClock(pause);
        change.PauseClock(ClockPauseReason.PatientUnavailable, null, Today.AddDays(20));
        change.StopClock(ClockStopReason.PathwayDischarged);
        return (referral, change.Events.ToList(), pause);
    }

    [Test]
    public void ToResponse_ClockEntries_AreWaitingTimeWithStructuredValuesForStaff()
    {
        var (_, events, _) = ClockEvents();

        var responses = events.Select(e => TimelineMapper.ToResponse(new ReferralTimelineRow(e) { PauseDetail = Canary }, isAdmin: false)).ToList();

        responses.Should().OnlyContain(r => r.Kind == nameof(PatientTimelineKind.WaitingTime));
        responses.Select(r => r.Action).Should().Equal(
            "ReferralTargetChanged", "ReferralClockPaused", "ReferralClockResumed", "ReferralClockPaused",
            "ReferralClockResumed", "ReferralClockStopped");
        responses[0].Should().BeEquivalentTo(new { PreviousTargetDays = (int?)28, TargetDays = (int?)14 });
        responses[1].Should().BeEquivalentTo(new { PauseReason = "Other", ReviewOn = (DateOnly?)Today.AddDays(10), Reason = (string?)null });
        responses[2].PauseReason.Should().Be("Other");
        responses[2].IsAutomatic.Should().BeFalse();
        responses[4].IsAutomatic.Should().BeTrue("the stop ended the pause");
        responses[5].Should().BeEquivalentTo(new
        {
            StopReason = "PathwayDischarged", DaysWaited = (int?)8, TargetDays = (int?)14, Breached = (bool?)false
        });
        responses.Should().OnlyContain(r => r.Reason == null);
    }

    [Test]
    public void ToResponse_ClockPaused_GivesThePausesDetailToTheFullViewOnly()
    {
        var (_, events, _) = ClockEvents();
        var paused = new ReferralTimelineRow(events[1]) { PauseDetail = Canary };

        TimelineMapper.ToResponse(paused, isAdmin: true).Reason.Should().Be(Canary);
        TimelineMapper.ToResponse(paused, isAdmin: false).Reason.Should().BeNull();
    }

    [Test]
    public void ToResponse_TargetDateMovedByTheClock_IsFlaggedFromClock()
    {
        var pathway = PathwaysTestConstants.CreatePathway(MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus.InProgress);
        var referral = CreateReferral(patient: pathway.Patient, status: ReferralStatus.Accepted);
        pathway.Referral = referral;
        var change = new PathwayChange(pathway, Registrar, NowUtc);
        change.PauseClock(ClockPauseReason.PatientChoseToWait, null, Today.AddDays(30));
        var handEdit = new PathwayChange(pathway, Registrar, NowUtc);
        PathwayDetails.Record(handEdit, new Dictionary<string, object> { ["dueDate"] = new { from = Today, to = Today.AddDays(1) } });

        var fromClock = TimelineMapper.ToResponse(new PatientTimelineRow(change.AuditEntries.Single(), pathway.Title), isAdmin: false);
        var byHand = TimelineMapper.ToResponse(new PatientTimelineRow(handEdit.AuditEntries.Single(), pathway.Title), isAdmin: false);

        fromClock.Kind.Should().Be(nameof(PatientTimelineKind.DueDates));
        fromClock.FromClock.Should().BeTrue();
        fromClock.Changes.Single().Should().BeEquivalentTo(new { Field = "dueDate", To = Today.AddDays(30).ToString("yyyy-MM-dd") });
        byHand.FromClock.Should().BeFalse();
    }

    [TestCase(null, 8)]
    [TestCase("WaitingTime", 6)]
    [TestCase("Referrals", 2)]
    [TestCase("Notes", 0)]
    public void Search_KindsFilter_SplitsTheClocksEntriesFromTheRest(string? kind, int count)
    {
        var (referral, events, _) = ClockEvents();
        var registered = new ReferralChange(referral, Registrar, NowUtc.AddDays(-8));
        registered.Register(referral.Team);
        registered.Withdraw(Detail);
        var all = events.Concat(registered.Events.Where(e => e.Action != ReferralEventAction.ClockStopped)).ToList();
        var query = new GetPatientTimelineQuery(referral.PatientId, null, 20, kind is null ? null : [kind], null, FullView: false);

        ReferralTimelineSearch.Page(all.AsQueryable(), new[] { referral }.AsQueryable(), query).ToList().Should().HaveCount(count);
    }

    [Test]
    public async Task Handle_FullView_ReadsEachPausesDetail_StaffViewDoesntAsk()
    {
        var (referral, events, pause) = ClockEvents();
        var referrals = new Mock<IReferralRepository>();
        referrals.Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(events.Select(e => new ReferralTimelineRow(e)).ToList());
        referrals.Setup(r => r.GetPauseDetailsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [pause.Id] = Canary });
        var handler = Handler(referral.PatientId, referrals.Object);

        var full = await handler.Handle(new GetPatientTimelineQuery(referral.PatientId, null, 20, null, null, FullView: true), CancellationToken.None);
        var staff = await handler.Handle(new GetPatientTimelineQuery(referral.PatientId, null, 20, null, null, FullView: false), CancellationToken.None);

        full.Items.Where(i => i.Action == "ReferralClockPaused").Select(i => i.Reason).Should().BeEquivalentTo([Canary, null]);
        staff.Items.Should().OnlyContain(i => i.Reason == null);
        referrals.Verify(r => r.GetPauseDetailsAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(pause.Id)), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GetPatientTimelineHandler Handler(Guid patientId, IReferralRepository referrals)
    {
        var fixture = AutoFixtureFactory.Create();
        var patients = new Mock<IPatientRepository>();
        patients.Setup(r => r.GetByIdAsync(patientId, It.IsAny<CancellationToken>())).ReturnsAsync(new Patient { Id = patientId });
        var audit = new Mock<IPathwayAuditRepository>();
        audit.Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var appointments = new Mock<IAppointmentRepository>();
        appointments.Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        fixture.Inject(patients.Object);
        fixture.Inject(audit.Object);
        fixture.Inject(appointments.Object);
        fixture.Inject(referrals);
        fixture.Inject<IActivityLog>(new RecordingActivityLog());
        return fixture.Create<GetPatientTimelineHandler>();
    }
}
