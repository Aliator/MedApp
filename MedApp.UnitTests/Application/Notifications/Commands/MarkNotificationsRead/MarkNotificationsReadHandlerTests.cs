using AutoFixture;
using FluentAssertions;
using MedApp.Application.Notifications.Commands.MarkNotificationsRead;
using MedApp.Application.Notifications.Repositories;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.NotificationsTestConstants;

namespace MedApp.UnitTests.Application.Notifications.Commands.MarkNotificationsRead;

[TestFixture]
public sealed class MarkNotificationsReadHandlerTests
{
    private Mock<INotificationRepository> _repository = null!;
    private MarkNotificationsReadHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<INotificationRepository>();
        fixture.Inject(_repository.Object);
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
        _handler = fixture.Create<MarkNotificationsReadHandler>();
    }

    [Test]
    public async Task Handle_OneNotification_MarksItReadNow()
    {
        using var cts = new CancellationTokenSource();
        _repository.Setup(r => r.MarkReadAsync(UserId, NotificationId, Now, cts.Token)).ReturnsAsync(1);

        var marked = await _handler.Handle(new MarkNotificationsReadCommand(UserId, NotificationId), cts.Token);

        marked.Should().Be(1);
    }

    [Test]
    public async Task Handle_NoNotificationGiven_MarksThemAllRead()
    {
        using var cts = new CancellationTokenSource();
        _repository.Setup(r => r.MarkReadAsync(UserId, null, Now, cts.Token)).ReturnsAsync(3);

        var marked = await _handler.Handle(new MarkNotificationsReadCommand(UserId, null), cts.Token);

        marked.Should().Be(3);
    }
}
