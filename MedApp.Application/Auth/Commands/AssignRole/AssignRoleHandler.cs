using MediatR;
using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Commands.AssignRole;

public sealed class AssignRoleHandler(
    IdentityUserRoleService userRoleService)
    : IRequestHandler<AssignRoleCommand, IdentityRole<Guid>?>
{
    public async Task<IdentityRole<Guid>?> Handle(
        AssignRoleCommand request,
        CancellationToken ct)
    {
        return await userRoleService.AssignRoleAsync(
            request.Username,
            request.Role,
            ct);
    }
}