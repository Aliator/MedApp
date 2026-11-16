using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.RevokeRole;

public sealed class RevokeRoleHandler(
    IIdentityRoleService userRoleService,
    IIdentityReadService readService,
    ISessionService sessionService)
    : IRequestHandler<RevokeRoleCommand, IdentityRole<Guid>?>
{
    public async Task<IdentityRole<Guid>?> Handle(
        RevokeRoleCommand request,
        CancellationToken ct)
    {
        var role = await userRoleService.RevokeRoleAsync(
            request.Username,
            request.Role,
            ct);

        // Null means nothing changed (no such user or role, or they didn't have it).
        if (role is not null)
            await sessionService.EndSessionsAsync(readService, request.Username, request.CurrentSessionId, ct);

        return role;
    }
}
