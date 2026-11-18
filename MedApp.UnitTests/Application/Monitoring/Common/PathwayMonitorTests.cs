using AutoFixture;
using FluentAssertions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Monitoring.Common;

[TestFixture]
public sealed class PathwayMonitorTests
{
    private Mock<IMonitoringRuleRepository> _rules = null!;
    private Mock<INotificationSender> _sender = null!;
    private Mock<ILogger<PathwayMonitor>> _logger = null!;
    private List<Notification> _sent = null!;
    private PathwayMonitor _monitor = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _rules = new Mock<IMonitoringRuleRepository>();
        _sender = new Mock<INotificationSender>();
        _logger = new Mock<ILogger<PathwayMonitor>>();
        _sent = [];

        _sender
            .Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => _sent.Add(n))
            .Returns(Task.CompletedTask);
        GivenAdmins(OwnerId, OtherOwnerId, Admin.UserId);

        fixture.Inject(_rules.Object);
        fixture.Inject(_sender.Object);
        fixture.Inject(_logger.Object);
        _monitor = fixture.Create<PathwayMonitor>();
    }

    private void GivenAdmins(params Guid[] ids) =>
        _rules
            .Setup(r => r.GetAdminsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> asked, CancellationToken _) => asked.Where(ids.Contains).ToHashSet());

    private void GivenRules(params MonitoringRule[] rules) =>
        _rules
            .Setup(r => r.GetEnabledAsync(It.IsAny<IReadOnlyCollection<MonitoringEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<MonitoringEvent> events, CancellationToken _) =>
                rules.Where(rule => rule.IsEnabled && events.Contains(rule.Event)).ToList());

    /// <summary>The staff member finishes the first stage of a High priority pathway.</summary>
    private static PathwayChange StaffFinishesFirstStage()
    {
        var pathway = CreatePathway(Status.InProgress, priority: PatientPathwayPriority.High);
        var change = new PathwayChange(pathway, Staff, Now);
        change.FinishStage(pathway.StageAt(1));
        return change;
    }

    [Test]
    public async Task NotifyAsync_RuleMatches_SendsItsOwnerOneNotificationLinkingThePathway()
    {
        GivenRules(CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId, minimumPriority: PatientPathwayPriority.High));
        var change = StaffFinishesFirstStage();
        using var cts = new CancellationTokenSource();

        await _monitor.NotifyAsync(change, cts.Token);

        _sent.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            UserId = OwnerId,
            Kind = NotificationKind.MonitoredStageFinished,
            PathwayId = (Guid?)change.Pathway.Id,
            CreatedAtUtc = Now,
            ReadAtUtc = (DateTime?)null,
        });
        _rules.Verify(r => r.GetEnabledAsync(
            It.Is<IReadOnlyCollection<MonitoringEvent>>(e => e.SequenceEqual(new[] { MonitoringEvent.StageFinished })), cts.Token), Times.Once);
        _rules.Verify(r => r.GetAdminsAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { OwnerId })), cts.Token), Times.Once);
        _sender.Verify(s => s.SendAsync(It.IsAny<Notification>(), cts.Token), Times.Once);
    }

    [Test]
    public async Task NotifyAsync_ChangeNoRuleCanWatch_ReadsNoRules()
    {
        var pathway = CreatePathway();
        var change = new PathwayChange(pathway, Staff, Now);
        change.UpdateStageNotes(pathway.StageAt(1), "Synthetic notes.");

        await _monitor.NotifyAsync(change, CancellationToken.None);

        _rules.VerifyNoOtherCalls();
        _sent.Should().BeEmpty();
    }

    [Test]
    public async Task NotifyAsync_NoRuleMatches_DoesNotLookUpAdmins()
    {
        GivenRules(CreateRule(MonitoringEvent.StageFinished, subjectUserId: OtherUserId));

        await _monitor.NotifyAsync(StaffFinishesFirstStage(), CancellationToken.None);

        _rules.Verify(r => r.GetAdminsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sent.Should().BeEmpty();
    }

    [Test]
    public async Task NotifyAsync_SeveralRulesOfSeveralAdmins_SendsOneToEachAdmin()
    {
        GivenRules(
            CreateRule(MonitoringEvent.StageFinished),
            CreateRule(MonitoringEvent.StageFinished, stageDefinitionId: ConsultationDefinitionId),
            CreateRule(MonitoringEvent.StageFinished, ownerId: OtherOwnerId),
            CreateRule(MonitoringEvent.StageFinished, ownerId: OtherOwnerId, isEnabled: false));

        await _monitor.NotifyAsync(StaffFinishesFirstStage(), CancellationToken.None);

        _sent.Select(n => n.UserId).Should().BeEquivalentTo([OwnerId, OtherOwnerId]);
    }

    [Test]
    public async Task NotifyAsync_OwnerMadeTheChange_TellsOnlyTheOtherAdmins()
    {
        GivenRules(CreateRule(MonitoringEvent.StageUnlocked, ownerId: Admin.UserId), CreateRule(MonitoringEvent.StageUnlocked));
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);
        var change = new PathwayChange(pathway, Admin, Now);
        change.UnlockStage(pathway.StageAt(1));

        await _monitor.NotifyAsync(change, CancellationToken.None);

        _sent.Should().ContainSingle().Which.UserId.Should().Be(OwnerId);
    }

    [Test]
    public async Task NotifyAsync_OwnerNoLongerAnAdmin_IsNotTold()
    {
        GivenAdmins(OtherOwnerId);
        GivenRules(CreateRule(MonitoringEvent.StageFinished), CreateRule(MonitoringEvent.StageFinished, ownerId: OtherOwnerId));

        await _monitor.NotifyAsync(StaffFinishesFirstStage(), CancellationToken.None);

        _sent.Should().ContainSingle().Which.UserId.Should().Be(OtherOwnerId);
    }

    [Test]
    public async Task NotifyAsync_RulesCantBeRead_LogsAndCarriesOn()
    {
        _rules
            .Setup(r => r.GetEnabledAsync(It.IsAny<IReadOnlyCollection<MonitoringEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable."));

        var act = () => _monitor.NotifyAsync(StaffFinishesFirstStage(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _sent.Should().BeEmpty();
        VerifyWarningLogged(Times.Once());
    }

    [Test]
    public async Task NotifyAsync_OneNotificationFails_StillSendsTheOthers()
    {
        GivenRules(CreateRule(MonitoringEvent.StageFinished), CreateRule(MonitoringEvent.StageFinished, ownerId: OtherOwnerId));
        _sender
            .Setup(s => s.SendAsync(It.Is<Notification>(n => n.UserId == OwnerId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable."));

        await _monitor.NotifyAsync(StaffFinishesFirstStage(), CancellationToken.None);

        _sent.Should().ContainSingle().Which.UserId.Should().Be(OtherOwnerId);
        VerifyWarningLogged(Times.Once());
    }

    [Test]
    public async Task NotifyAsync_RequestCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        _rules
            .Setup(r => r.GetEnabledAsync(It.IsAny<IReadOnlyCollection<MonitoringEvent>>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _monitor.NotifyAsync(StaffFinishesFirstStage(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task NotifyAsync_RequestCancelledWhileSending_Throws()
    {
        using var cts = new CancellationTokenSource();
        GivenRules(CreateRule(MonitoringEvent.StageFinished));
        _sender
            .Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _monitor.NotifyAsync(StaffFinishesFirstStage(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task NotifyAsync_TimeoutWhileTheRequestGoesOn_LogsAndCarriesOn()
    {
        // An OperationCanceledException of its own (a command timeout), not the request's: the change stands.
        GivenRules(CreateRule(MonitoringEvent.StageFinished), CreateRule(MonitoringEvent.StageFinished, ownerId: OtherOwnerId));
        _sender
            .Setup(s => s.SendAsync(It.Is<Notification>(n => n.UserId == OwnerId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await _monitor.NotifyAsync(StaffFinishesFirstStage(), CancellationToken.None);

        _sent.Should().ContainSingle().Which.UserId.Should().Be(OtherOwnerId);
        VerifyWarningLogged(Times.Once());
    }

    [Test]
    public async Task NotifyAsync_AdminsCantBeRead_LogsAndCarriesOn()
    {
        GivenRules(CreateRule(MonitoringEvent.StageFinished));
        _rules
            .Setup(r => r.GetAdminsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await _monitor.NotifyAsync(StaffFinishesFirstStage(), CancellationToken.None);

        _sent.Should().BeEmpty();
        VerifyWarningLogged(Times.Once());
    }

    private void VerifyWarningLogged(Times times) =>
        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);
}
