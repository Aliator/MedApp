using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.AssignRole;

/// <summary>Gives a user a role; the activity log records it as a roles change.</summary>
public sealed class AssignRoleHandler(
    IIdentityRoleService userRoleService,
    IActivityLog activityLog)
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

        if (role is not null)
            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.RolesChanged, targetUsername: request.Username, details: new { added = role.Name }),
                ct);

        return role;
    }
}
