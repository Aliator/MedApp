using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.CreateRole;

/// <summary>Creates a role; the activity log records it.</summary>
public sealed class CreateRoleHandler(
    IIdentityRoleService roleService,
    IActivityLog activityLog)
    : IRequestHandler<CreateRoleCommand, IdentityRole<Guid>?>
{
    public async Task<IdentityRole<Guid>?> Handle(
        CreateRoleCommand request,
        CancellationToken ct)
    {
        var role = await roleService.CreateRoleAsync(request.Name, ct);

        if (role is not null)
            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.RoleCreated, details: new { role = role.Name }),
                ct);

        return role;
    }
}
