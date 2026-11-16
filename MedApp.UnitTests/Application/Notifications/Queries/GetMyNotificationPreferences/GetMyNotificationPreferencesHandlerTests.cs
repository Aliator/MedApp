using AutoFixture;
using FluentAssertions;
using MedApp.Application.Notifications.Queries.GetMyNotificationPreferences;
using MedApp.Application.Notifications.Repositories;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.NotificationsTestConstants;

namespace MedApp.UnitTests.Application.Notifications.Queries.GetMyNotificationPreferences;

[TestFixture]
public sealed class GetMyNotificationPreferencesHandlerTests
{
    private Mock<INotificationPreferencesRepository> _repository = null!;
    private GetMyNotificationPreferencesHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<INotificationPreferencesRepository>();
        fixture.Inject(_repository.Object);
        _handler = fixture.Create<GetMyNotificationPreferencesHandler>();
    }

    [Test]
    public async Task Handle_NeverChosen_ReturnsTheDefaults()
    {
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(new GetMyNotificationPreferencesQuery(UserId), cts.Token);

        response.Should().Be(new NotificationPreferencesResponse(true));
        _repository.Verify(r => r.GetAsync(UserId, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_Chosen_ReturnsTheirChoice()
    {
        _repository.Setup(r => r.GetAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationPreferences { UserId = UserId, ProcedureAssigned = false });

        var response = await _handler.Handle(new GetMyNotificationPreferencesQuery(UserId), CancellationToken.None);

        response.ProcedureAssigned.Should().BeFalse();
    }
}
