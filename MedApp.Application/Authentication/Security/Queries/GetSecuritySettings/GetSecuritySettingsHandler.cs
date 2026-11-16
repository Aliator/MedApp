using MedApp.Application.Authentication.Security.Common;
using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Security.Queries.GetSecuritySettings;

public sealed class GetSecuritySettingsHandler(
    ISecuritySettingsRepository settings,
    IIdentityReadService readService,
    ITwoFactorService twoFactor)
    : IRequestHandler<GetSecuritySettingsQuery, SecuritySettingsResponse>
{
    public Task<SecuritySettingsResponse> Handle(GetSecuritySettingsQuery request, CancellationToken ct) =>
        SecuritySettingsResponses.LoadAsync(settings, readService, twoFactor, ct);
}
