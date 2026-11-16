using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Security.Commands.SaveSecuritySettings;

/// <summary>An admin (<paramref name="ActorUserId"/>) sets the deployment's sign-in rules.</summary>
public sealed record SaveSecuritySettingsCommand(
    bool RequireTwoFactorForAdmins,
    Guid ActorUserId,
    string ActorUsername
) : IRequest<SecuritySettingsResponse>;
