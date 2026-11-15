using AutoFixture;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Notifications.Repositories;
using MedApp.Contracts.Notifications.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.NotificationsTestConstants;

namespace MedApp.UnitTests.Application.Notifications.Common;

[TestFixture]
public sealed class NotificationSenderTests
{
    private Mock<INotificationRepository> _repository = null!;
    private Mock<INotificationPublisher> _publisher = null!;
    private NotificationSender _sender = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<INotificationRepository>();
        _publisher = new Mock<INotificationPublisher>();
        fixture.Inject(_repository.Object);
        fixture.Inject(_publisher.Object);
        _sender = fixture.Create<NotificationSender>();
    }

    [Test]
    public async Task SendAsync_StoresItThenPushesItToItsUser()
    {
        using var cts = new CancellationTokenSource();
        var notification = CreateNotification();
        var order = new List<string>();
        _repository.Setup(r => r.AddAsync(notification, cts.Token)).Callback(() => order.Add("stored")).Returns(Task.CompletedTask);
        _publisher
            .Setup(p => p.PublishAsync(UserId, It.Is<NotificationResponse>(n => n.Id == NotificationId && !n.IsRead), cts.Token))
            .Callback(() => order.Add("pushed"))
            .Returns(Task.CompletedTask);

        await _sender.SendAsync(notification, cts.Token);

        _repository.Verify(r => r.AddAsync(notification, cts.Token), Times.Once);
        _publisher.Verify(p => p.PublishAsync(UserId, It.IsAny<NotificationResponse>(), cts.Token), Times.Once);
        Assert.That(order, Is.EqualTo(new[] { "stored", "pushed" }));
    }
}
