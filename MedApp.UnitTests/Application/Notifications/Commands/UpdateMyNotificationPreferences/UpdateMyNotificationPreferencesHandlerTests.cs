using AutoFixture;
using FluentAssertions;
using MedApp.Application.Notifications.Commands.UpdateMyNotificationPreferences;
using MedApp.Application.Notifications.Repositories;
using MedApp.Contracts.Notifications.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.NotificationsTestConstants;

namespace MedApp.UnitTests.Application.Notifications.Commands.UpdateMyNotificationPreferences;

[TestFixture]
public sealed class UpdateMyNotificationPreferencesHandlerTests
{
    private Mock<INotificationPreferencesRepository> _repository = null!;
    private UpdateMyNotificationPreferencesHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<INotificationPreferencesRepository>();
        fixture.Inject(_repository.Object);
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
        _handler = fixture.Create<UpdateMyNotificationPreferencesHandler>();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Handle_SavesTheirChoiceNowAndReturnsIt(bool pathwayAssigned)
    {
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(new UpdateMyNotificationPreferencesCommand(UserId, pathwayAssigned), cts.Token);

        _repository.Verify(r => r.SaveAsync(UserId, pathwayAssigned, Now, cts.Token), Times.Once);
        response.Should().Be(new NotificationPreferencesResponse(pathwayAssigned));
    }
}
