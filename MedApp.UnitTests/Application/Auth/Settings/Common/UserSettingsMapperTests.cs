using FluentAssertions;
using MedApp.Application.Authentication.Settings.Common;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Settings.Common;

[TestFixture]
public sealed class UserSettingsMapperTests
{
    [Test]
    public void ToResponse_SavedSettings_ReturnsThemAndWhenTheyWereSaved()
    {
        var settings = new UserSettings(AuthTestConstants.SettingsUserId, AuthTestConstants.SettingsJson, AuthTestConstants.SettingsSavedAtUtc);

        UserSettingsMapper.ToResponse(settings)
            .Should().Be(new UserSettingsResponse(AuthTestConstants.SettingsJson, AuthTestConstants.SettingsSavedAtUtc));
    }

    [Test]
    public void ToResponse_NoneSaved_ReturnsAnEmptyResponse()
    {
        UserSettingsMapper.ToResponse(null).Should().Be(new UserSettingsResponse(null, null));
    }
}
