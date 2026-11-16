using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Authentication.TwoFactor.Common;

/// <summary>Whether an account must use two-factor authentication: an admin, while admins are required to.</summary>
public static class TwoFactorRequirement
{
    public static async Task<bool> IsRequiredAsync(
        this ISecuritySettingsRepository settings,
        TwoFactorState state,
        CancellationToken ct) =>
        state.IsAdmin && (await settings.GetAsync(ct)).RequireTwoFactorForAdmins;
}
