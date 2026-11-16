using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;

namespace MedApp.Application.Authentication.Security.Common;

/// <summary>Builds the settings' response, looking up who last changed them and counting admins without two-factor.</summary>
public static class SecuritySettingsResponses
{
    public static async Task<SecuritySettingsResponse> LoadAsync(
        ISecuritySettingsRepository repository,
        IIdentityReadService readService,
        ITwoFactorService twoFactor,
        CancellationToken ct)
    {
        var settings = await repository.GetAsync(ct);

        string? updatedBy = null;
        if (settings.UpdatedByUserId is { } updatedById)
        {
            var names = await readService.GetUsernamesByIdAsync([updatedById], ct);
            updatedBy = names.GetValueOrDefault(updatedById);
        }

        return SecuritySettingsMapper.ToResponse(settings, updatedBy, await twoFactor.GetAdminsWithoutTwoFactorAsync(ct));
    }
}
