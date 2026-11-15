using FluentAssertions;
using MedApp.Application.Authentication.Settings.Commands.SaveMySettings;
using MedApp.Application.Authentication.Settings.Repositories;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Settings.Commands.SaveMySettings;

[TestFixture]
public sealed class SaveMySettingsHandlerTests
{
    [Test]
    public async Task Handle_SavesTheSettingsForTheUserAtTheCurrentTime()
    {
        var repository = new Mock<IUserSettingsRepository>();
        var handler = new SaveMySettingsHandler(repository.Object, new FixedTimeProvider(AuthTestConstants.SettingsSavedAtUtc));
        using var cts = new CancellationTokenSource();

        await handler.Handle(new SaveMySettingsCommand(AuthTestConstants.SettingsUserId, AuthTestConstants.SettingsJson), cts.Token);

        repository.Verify(r => r.SaveAsync(
            AuthTestConstants.SettingsUserId,
            AuthTestConstants.SettingsJson,
            AuthTestConstants.SettingsSavedAtUtc,
            cts.Token), Times.Once);
    }
}
