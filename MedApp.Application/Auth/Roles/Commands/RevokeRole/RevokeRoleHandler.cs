using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Roles.Commands.RevokeRole;

public sealed class RevokeRoleHandler(
    IIdentityRoleService userRoleService)
    : IRequestHandler<RevokeRoleCommand, IdentityRole<Guid>?>
{
    public async Task<IdentityRole<Guid>?> Handle(
        RevokeRoleCommand request,
        CancellationToken ct)
    {
        return await userRoleService.RevokeRoleAsync(
            request.Username,
            request.Role,
            ct);
    }
}