using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.Security.Common;

/// <summary>The deployment's sign-in rules as the API returns them to admins.</summary>
public static class SecuritySettingsMapper
{
    /// <param name="updatedBy">The username of the admin who last changed them, if their account still exists.</param>
    /// <param name="adminsWithoutTwoFactor">How many admin accounts don't have two-factor on.</param>
    public static SecuritySettingsResponse ToResponse(SecuritySettings settings, string? updatedBy, int adminsWithoutTwoFactor) =>
        new(settings.RequireTwoFactorForAdmins, adminsWithoutTwoFactor, settings.UpdatedAtUtc, updatedBy);
}
