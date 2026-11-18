using FluentAssertions;
using MedApp.API.Notifications;
using MedApp.Contracts.Notifications.Responses;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Notifications;

[TestFixture]
public sealed class SignalRNotificationPublisherTests
{
    private static readonly NotificationResponse Notification =
        new(RecordId, "PathwayAssigned", "Synthetic pathway", "test.admin assigned you to this pathway.", OtherId, ExpiresAtUtc, false);

    private Mock<IClientProxy> _client = null!;
    private Mock<IHubContext<NotificationsHub>> _hub = null!;

    [SetUp]
    public void SetUp()
    {
        _client = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.User(UserId.ToString())).Returns(_client.Object);
        _hub = new Mock<IHubContext<NotificationsHub>>();
        _hub.SetupGet(h => h.Clients).Returns(clients.Object);
    }

    [Test]
    public async Task PublishAsync_SendsItToTheUsersOpenPages()
    {
        using var cts = new CancellationTokenSource();

        await new SignalRNotificationPublisher(_hub.Object, NullLogger<SignalRNotificationPublisher>.Instance)
            .PublishAsync(UserId, Notification, cts.Token);

        _client.Verify(c => c.SendCoreAsync(
            NotificationsHub.NotificationMethod,
            It.Is<object?[]>(args => args.Length == 1 && Equals(args[0], Notification)),
            cts.Token), Times.Once);
    }

    [Test]
    public async Task PublishAsync_SendingFails_DoesNotThrow()
    {
        _client
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Hub unavailable"));

        var act = () => new SignalRNotificationPublisher(_hub.Object, NullLogger<SignalRNotificationPublisher>.Instance)
            .PublishAsync(UserId, Notification, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
