using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.AssignRole;

public sealed class AssignRoleHandler(
    IIdentityRoleService userRoleService,
    IIdentityReadService readService,
    ISessionService sessionService)
    : IRequestHandler<AssignRoleCommand, IdentityRole<Guid>?>
{
    public async Task<IdentityRole<Guid>?> Handle(
        AssignRoleCommand request,
        CancellationToken ct)
    {
        var role = await userRoleService.AssignRoleAsync(
            request.Username,
            request.Role,
            ct);

        // Null means nothing changed (no such user or role, or they had it already).
        if (role is not null)
            await sessionService.EndSessionsAsync(readService, request.Username, request.CurrentSessionId, ct);

        return role;
    }
}
