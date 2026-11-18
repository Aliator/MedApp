using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.RevokeRole;

/// <summary>
/// Takes a role from a user; the activity log records it as a roles change. Refused (null) for your own roles, as
/// <c>SetUserRoles</c> refuses them, and for Admin from the last active admin (<see cref="ILastAdminGuard"/>).
/// </summary>
public sealed class RevokeRoleHandler(
    IIdentityRoleService userRoleService,
    IIdentityReadService readService,
    ILastAdminGuard lastAdmin,
    IActivityLog activityLog)
    : IRequestHandler<RevokeRoleCommand, IdentityRole<Guid>?>
{
    public async Task<IdentityRole<Guid>?> Handle(
        RevokeRoleCommand request,
        CancellationToken ct)
    {
        if (string.Equals(request.Username, request.ActorUsername, StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.Equals(request.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
            && await readService.GetUserAsync(request.Username, ct) is { } user
            && await lastAdmin.WouldLeaveNoAdminAsync(user.UserId, ct))
            return null;

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
