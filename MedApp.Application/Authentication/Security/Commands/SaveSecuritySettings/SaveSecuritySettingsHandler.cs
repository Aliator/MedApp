using MedApp.Application.Authentication.Security.Common;
using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Security.Commands.SaveSecuritySettings;

/// <summary>
/// Saves the rules and records who changed them. Requiring two-factor for admins is refused (409) until the admin
/// turning it on uses it themselves, so nobody switches it on and is then held out of the admin pages. Admins without
/// it are held to set it up from their next request (<c>TwoFactorSetupRequired</c>).
/// </summary>
public sealed class SaveSecuritySettingsHandler(
    ISecuritySettingsRepository settings,
    IIdentityReadService readService,
    ITwoFactorService twoFactor,
    ISecurityEventLog events,
    TimeProvider timeProvider)
    : IRequestHandler<SaveSecuritySettingsCommand, SecuritySettingsResponse>
{
    public async Task<SecuritySettingsResponse> Handle(SaveSecuritySettingsCommand request, CancellationToken ct)
    {
        var current = await settings.GetAsync(ct);

        if (request.RequireTwoFactorForAdmins
            && !current.RequireTwoFactorForAdmins
            && !await twoFactor.IsEnabledAsync(request.ActorUserId, ct))
            throw new ConflictException(
                "Set up two-factor authentication for your own account first (Account Settings, Security), then require it for admins.");

        var changed = current.RequireTwoFactorForAdmins != request.RequireTwoFactorForAdmins;
        current.RequireTwoFactorForAdmins = request.RequireTwoFactorForAdmins;
        current.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        current.UpdatedByUserId = request.ActorUserId;
        await settings.SaveAsync(current, ct);

        if (changed)
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.TwoFactorRequirementChanged, request.ActorUserId, request.ActorUsername,
                    Details: new Dictionary<string, object?> { ["requireTwoFactorForAdmins"] = request.RequireTwoFactorForAdmins }),
                ct);

        return await SecuritySettingsResponses.LoadAsync(settings, readService, twoFactor, ct);
    }
}
