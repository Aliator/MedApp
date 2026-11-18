using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Monitoring.ServiceAlerts;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;

namespace MedApp.UnitTests.Application.Monitoring.ServiceAlerts;

[TestFixture]
public sealed class ServiceAlertsJobTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private Mock<IMonitoringRuleRepository> _rules = null!;
    private Mock<IServiceAlertRepository> _alerts = null!;
    private Mock<IIdentityReadService> _identity = null!;
    private Mock<INotificationPublisher> _publisher = null!;
    private List<MonitoringRule> _enabled = null!;
    private ServiceAlertsJob _job = null!;

    [SetUp]
    public void SetUp()
    {
        _rules = new Mock<IMonitoringRuleRepository>();
        _alerts = new Mock<IServiceAlertRepository>();
        _identity = new Mock<IIdentityReadService>();
        _publisher = new Mock<INotificationPublisher>();
        _enabled = [];

        _rules.Setup(r => r.GetEnabledAsync(It.IsAny<IReadOnlyCollection<MonitoringEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _enabled);
        _rules.Setup(r => r.GetOwnersWhoCanMonitorAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { OwnerId });
        _alerts.Setup(a => a.GetTeamNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [RuleTeamId] = RuleTeamName });

        _job = new ServiceAlertsJob(_rules.Object, _alerts.Object, _identity.Object, _publisher.Object, NullLogger<ServiceAlertsJob>.Instance);
    }

    [Test]
    public async Task RunAsync_NoRules_DoesNothing()
    {
        (await _job.RunAsync(Now, CancellationToken.None)).Should().Be(0);

        _alerts.Verify(a => a.FindNewAsync(It.IsAny<MonitoringRule>(), It.IsAny<DateTime>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_RuleFindsItems_RecordsThemWithOneNotification_AndPushesIt()
    {
        var rule = CreateRule(MonitoringEvent.ReferralNotTriaged, thresholdDays: 2, teamId: RuleTeamId);
        _enabled.Add(rule);
        var items = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _alerts.Setup(a => a.FindNewAsync(rule, Now, new DateOnly(2026, 6, 10), It.IsAny<CancellationToken>())).ReturnsAsync(items);

        (await _job.RunAsync(Now, CancellationToken.None)).Should().Be(3);

        _alerts.Verify(a => a.RecordAsync(
            rule.Id,
            items,
            It.Is<Notification>(n => n.UserId == OwnerId
                                     && n.Kind == NotificationKind.MonitoredReferralNotTriaged
                                     && n.Message == "3 Dermatology referrals have waited more than 2 days for triage."),
            Now,
            It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.PublishAsync(OwnerId, It.IsAny<NotificationResponse>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAsync_NothingNew_SendsNothing()
    {
        var rule = CreateRule(MonitoringEvent.VisitNotBooked, thresholdDays: 2);
        _enabled.Add(rule);
        _alerts.Setup(a => a.FindNewAsync(rule, Now, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        (await _job.RunAsync(Now, CancellationToken.None)).Should().Be(0);

        _alerts.Verify(a => a.RecordAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Notification>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_OwnerNoLongerMonitors_SkipsTheirRule()
    {
        _enabled.Add(CreateRule(MonitoringEvent.SpecimenOverdue, ownerId: OtherOwnerId, thresholdDays: 0));

        (await _job.RunAsync(Now, CancellationToken.None)).Should().Be(0);

        _alerts.Verify(a => a.FindNewAsync(It.IsAny<MonitoringRule>(), It.IsAny<DateTime>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_PushFails_StillCountsWhatWasRecorded()
    {
        var rule = CreateRule(MonitoringEvent.WaitBreached, thresholdDays: 0);
        _enabled.Add(rule);
        _alerts.Setup(a => a.FindNewAsync(rule, Now, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([Guid.NewGuid()]);
        _publisher.Setup(p => p.PublishAsync(It.IsAny<Guid>(), It.IsAny<NotificationResponse>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        (await _job.RunAsync(Now, CancellationToken.None)).Should().Be(1);
    }
}
