using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.RevokeRole;

/// <summary>
/// Takes a role from a user; the activity log records it as a roles change. Refused (null) for your own roles, as
/// <c>SetUserRoles</c> refuses them, and for Admin from the last active admin (<see cref="ILastAdminGuard"/>). The check,
/// the change and its log entry run in one transaction, which holds the guard's lock, so two admins revoking each
/// other's Admin at once can't both succeed.
/// </summary>
public sealed class RevokeRoleHandler(
    IIdentityRoleService userRoleService,
    IIdentityReadService readService,
    ILastAdminGuard lastAdmin,
    IIdentityTransactions transactions,
    IActivityLog activityLog)
    : IRequestHandler<RevokeRoleCommand, IdentityRole<Guid>?>
{
    public async Task<IdentityRole<Guid>?> Handle(
        RevokeRoleCommand request,
        CancellationToken ct)
    {
        if (string.Equals(request.Username, request.ActorUsername, StringComparison.OrdinalIgnoreCase))
            return null;

        var user = string.Equals(request.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? await readService.GetUserAsync(request.Username, ct)
            : null;

        IdentityRole<Guid>? role = null;
        // A retry runs it all again, from the database as it was.
        await transactions.RunAsync(async () =>
        {
            role = null;
            if (user is not null && await lastAdmin.WouldLeaveNoAdminAsync(user.UserId, ct))
                return IdentityResult.Failed(IdentityErrors.LastAdmin);

            role = await userRoleService.RevokeRoleAsync(request.Username, request.Role, ct);
            if (role is null)
                return IdentityResult.Failed(IdentityErrors.RoleNotFound);

            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.RolesChanged, targetUsername: request.Username, details: new { removed = role.Name }),
                ct);
            return IdentityResult.Success;
        }, ct);

        return role;
    }
}
