using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.AssignRole;

/// <summary>
/// Gives a user a role; the activity log records it as a roles change. Someone in a team can't be given Admin (a 409):
/// an admin is in no team.
/// </summary>
public sealed class AssignRoleHandler(
    IIdentityRoleService userRoleService,
    IActivityLog activityLog,
    IIdentityReadService readService,
    ITeamRepository teams)
    : IRequestHandler<AssignRoleCommand, IdentityRole<Guid>?>
{
    public async Task<IdentityRole<Guid>?> Handle(
        AssignRoleCommand request,
        CancellationToken ct)
    {
        if (string.Equals(request.Role, AppRoles.Admin, StringComparison.Ordinal)
            && await readService.GetUserAsync(request.Username, ct) is { } user
            && (await teams.GetForUserAsync(user.UserId, ct)).Count > 0)
            throw new ConflictException(TeamRules.TeamMemberIsNoAdmin(user.Username));

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
