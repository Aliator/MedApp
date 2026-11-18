using AutoFixture;
using FluentAssertions;
using MedApp.Application.Notifications.Queries.GetMyNotifications;
using MedApp.Application.Notifications.Repositories;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.NotificationsTestConstants;

namespace MedApp.UnitTests.Application.Notifications.Queries.GetMyNotifications;

[TestFixture]
public sealed class GetMyNotificationsHandlerTests
{
    private Mock<INotificationRepository> _repository = null!;
    private GetMyNotificationsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<INotificationRepository>();
        fixture.Inject(_repository.Object);
        _handler = fixture.Create<GetMyNotificationsHandler>();
    }

    [Test]
    public async Task Handle_HasNotifications_ReturnsThemAndTheUnreadCount()
    {
        using var cts = new CancellationTokenSource();
        _repository.Setup(r => r.GetRecentAsync(UserId, 20, cts.Token)).ReturnsAsync([CreateNotification(), CreateNotification(Now)]);
        _repository.Setup(r => r.CountUnreadAsync(UserId, cts.Token)).ReturnsAsync(7);

        var result = await _handler.Handle(new GetMyNotificationsQuery(UserId), cts.Token);

        result.UnreadCount.Should().Be(7);
        result.Items.Should().HaveCount(2);
        var first = result.Items[0];
        first.Id.Should().Be(NotificationId);
        first.Kind.Should().Be("PathwayAssigned");
        first.Title.Should().Be(Title);
        first.Message.Should().Be(Message);
        first.PathwayId.Should().Be(PathwayId);
        first.CreatedAtUtc.Should().Be(CreatedAtUtc);
        first.IsRead.Should().BeFalse();
        result.Items[1].IsRead.Should().BeTrue();
    }

    [TestCase(0, 1)]
    [TestCase(500, GetMyNotificationsQuery.MaxTake)]
    [TestCase(5, 5)]
    public async Task Handle_TakeOutOfRange_ClampsIt(int take, int expected)
    {
        _repository.Setup(r => r.GetRecentAsync(UserId, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _handler.Handle(new GetMyNotificationsQuery(UserId, take), CancellationToken.None);

        _repository.Verify(r => r.GetRecentAsync(UserId, expected, It.IsAny<CancellationToken>()), Times.Once);
    }
}
