using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.DeleteRole;

/// <summary>Deletes a role; the activity log records it.</summary>
public sealed class DeleteRoleHandler(
    IIdentityRoleService roleService,
    IActivityLog activityLog)
    : IRequestHandler<DeleteRoleCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        DeleteRoleCommand request,
        CancellationToken ct)
    {
        var result = await roleService.DeleteRoleAsync(
            request.RoleName,
            ct);

        if (result.Succeeded)
            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.RoleDeleted, details: new { role = request.RoleName }),
                ct);

        return result;
    }
}
