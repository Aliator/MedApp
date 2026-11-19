using FluentAssertions;
using MedApp.Application.Monitoring.ServiceAlerts;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;

namespace MedApp.UnitTests.Application.Monitoring.ServiceAlerts;

[TestFixture]
public sealed class ServiceAlertNotificationsTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Create_ReferralNotTriaged_GivesTheCountTeamAndDays_AndLinksTheTeam()
    {
        var rule = CreateRule(MonitoringEvent.ReferralNotTriaged, thresholdDays: 2, teamId: RuleTeamId);

        var notification = ServiceAlertNotifications.Create(rule, 3, RuleTeamName, null, Now);

        notification.Should().BeEquivalentTo(new
        {
            UserId = OwnerId,
            Kind = NotificationKind.MonitoredReferralNotTriaged,
            Title = "Referrals awaiting triage",
            Message = "3 Dermatology referrals have waited more than 2 days for triage.",
            SubjectId = (Guid?)RuleTeamId,
            PathwayId = (Guid?)null,
            CreatedAtUtc = Now,
        });
    }

    [TestCase(MonitoringEvent.ReferralNotTriaged, 1, 0, "1 referral received before today is awaiting triage.")]
    [TestCase(MonitoringEvent.ResultNotSignedOff, 2, 1, "2 results for sam have awaited sign-off for more than 1 day.")]
    [TestCase(MonitoringEvent.VisitNotBooked, 4, 3, "4 visits have waited more than 3 days to be booked.")]
    [TestCase(MonitoringEvent.SpecimenOverdue, 1, 0, "1 sample is past its expected turnaround.")]
    [TestCase(MonitoringEvent.SpecimenOverdue, 2, 2, "2 samples are more than 2 days past their expected turnaround.")]
    [TestCase(MonitoringEvent.WaitBreached, 5, 0, "5 referrals have passed their waiting-time target.")]
    [TestCase(MonitoringEvent.WaitBreached, 1, 7, "1 referral is within 7 days of its waiting-time target, or past it.")]
    [TestCase(MonitoringEvent.PauseReviewPassed, 2, 0, "2 paused referrals are past their review date.")]
    public void MessageOf_EachAlert_SaysWhatItFound(MonitoringEvent monitoringEvent, int count, int days, string expected)
    {
        var who = monitoringEvent == MonitoringEvent.ResultNotSignedOff ? "sam" : null;

        ServiceAlertNotifications.MessageOf(monitoringEvent, count, days, null, who).Should().Be(expected);
    }

    [Test]
    public void KindOf_EveryServiceAlert_IsAMonitoringKindWithAFixedTitle()
    {
        foreach (var monitoringEvent in MonitoringEvents.ServiceAlerts)
        {
            var kind = ServiceAlertNotifications.KindOf(monitoringEvent);
            Notification.IsMonitoring(kind).Should().BeTrue();
            Notification.HasFixedTitle(kind).Should().BeTrue();
            ServiceAlertNotifications.TitleOf(monitoringEvent).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Test]
    public void ServiceAlerts_AreEightToThirteen_AndTheLabClocks_WithTheirKinds()
    {
        MonitoringEvents.ServiceAlerts.Select(e => (int)e).Should().Equal(8, 9, 10, 11, 12, 13, 16, 17);
        MonitoringEvents.ServiceAlerts.Select(e => (int)ServiceAlertNotifications.KindOf(e)).Should().Equal(32, 33, 34, 35, 36, 37, 47, 48);
        MonitoringEvents.IsServiceAlert(MonitoringEvent.LabCaseOverdue).Should().BeTrue();
        MonitoringEvents.IsLabClock(MonitoringEvent.SpecimenOverdue).Should().BeFalse();
        MonitoringEvents.IsServiceAlert(MonitoringEvent.StageFinishedWithoutVisit).Should().BeFalse();
    }
}
