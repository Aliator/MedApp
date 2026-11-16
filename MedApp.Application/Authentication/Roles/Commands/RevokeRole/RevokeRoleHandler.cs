using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.RevokeRole;

/// <summary>Takes a role from a user; the activity log records it as a roles change.</summary>
public sealed class RevokeRoleHandler(
    IIdentityRoleService userRoleService,
    IActivityLog activityLog)
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

        if (role is not null)
            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.RolesChanged, targetUsername: request.Username, details: new { removed = role.Name }),
                ct);

        return role;
    }
}
