using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.Settings.Common;

public static class UserSettingsMapper
{
    /// <summary>A user's saved settings, or an empty response (null settings) when they've never saved any.</summary>
    public static UserSettingsResponse ToResponse(UserSettings? settings) =>
        new(settings?.Json, settings?.UpdatedAtUtc);
}
