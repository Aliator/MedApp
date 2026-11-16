using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.SetUserRoles;

/// <summary>Sets a user's roles; the activity log records the change, with the roles before and after.</summary>
public sealed class SetUserRolesHandler(
    IIdentityRoleService roleService,
    IIdentityReadService readService,
    IActivityLog activityLog)
    : IRequestHandler<SetUserRolesCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        SetUserRolesCommand request,
        CancellationToken ct)
    {
        // An admin could otherwise take away their own Admin role, and with it the way back.
        if (string.Equals(request.Username, request.ActorUsername, StringComparison.OrdinalIgnoreCase))
            return IdentityResult.Failed(IdentityErrors.OwnRoles);

        var before = await readService.GetRolesForUserAsync(request.Username, ct);

        var result = await roleService.SetRolesAsync(request.Username, request.Roles, ct);

        if (result.Succeeded)
            await activityLog.RecordAsync(
                activityLog.Entry(
                    ActivityAction.RolesChanged,
                    targetUsername: request.Username,
                    details: new { from = before, to = request.Roles }),
                ct);

        return result;
    }
}
