using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Settings.Queries.GetMySettings;
using MedApp.Application.Authentication.Settings.Repositories;
using MedApp.Domain.Authentication;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Settings.Queries.GetMySettings;

[TestFixture]
public sealed class GetMySettingsHandlerTests
{
    private Mock<IUserSettingsRepository> _repository = null!;
    private GetMySettingsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IUserSettingsRepository>();
        fixture.Inject(_repository.Object);
        _handler = fixture.Create<GetMySettingsHandler>();
    }

    [Test]
    public async Task Handle_SettingsSaved_ReturnsThem()
    {
        using var cts = new CancellationTokenSource();
        _repository
            .Setup(r => r.GetAsync(AuthTestConstants.SettingsUserId, cts.Token))
            .ReturnsAsync(new UserSettings(AuthTestConstants.SettingsUserId, AuthTestConstants.SettingsJson, AuthTestConstants.SettingsSavedAtUtc));

        var result = await _handler.Handle(new GetMySettingsQuery(AuthTestConstants.SettingsUserId), cts.Token);

        result.Settings.Should().Be(AuthTestConstants.SettingsJson);
        result.UpdatedAtUtc.Should().Be(AuthTestConstants.SettingsSavedAtUtc);
    }

    [Test]
    public async Task Handle_NoneSaved_ReturnsNulls()
    {
        _repository
            .Setup(r => r.GetAsync(AuthTestConstants.SettingsUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSettings?)null);

        var result = await _handler.Handle(new GetMySettingsQuery(AuthTestConstants.SettingsUserId), CancellationToken.None);

        result.Settings.Should().BeNull();
        result.UpdatedAtUtc.Should().BeNull();
    }
}
