using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Roles.Responses;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.UpdateRole;

/// <summary>
/// Changes a role's name, description and permissions. The built-in Admin role refuses (409); an Administrator-only
/// permission was refused by the validator (400). The change and its <c>RoleChanged</c> entry (the permission names
/// added and removed) are saved in one transaction, from the version the admin saw: if another admin saved first, it's
/// a 409 and nothing is saved. The other admins are then emailed. A save that changes nothing records nothing.
/// Everyone who holds the role has its new permissions from their next request.
/// </summary>
public sealed class UpdateRoleHandler(
    IRoleRepository roles,
    IIdentityTransactions transactions,
    ISecurityEventLog events,
    ISecurityNotices notices,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateRoleCommand, RoleResponse>
{
    public async Task<RoleResponse> Handle(UpdateRoleCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var description = request.Description?.Trim() ?? string.Empty;
        var permissions = RoleRules.Requested(request.Permissions);

        RoleData? saved = null;
        RoleChange? change = null;
        // A retry runs it all again, from the database as it was.
        await transactions.RunAsync(async () =>
        {
            var role = await roles.GetOrThrowAsync(request.RoleId, ct);
            RoleRules.EnsureEditable(role);
            RoleRules.EnsureVersion(role, request.ExpectedVersion);

            saved = role;
            change = RoleChange.Between(role, name, description, permissions);
            if (!change.Any)
                return IdentityResult.Success;

            if (change.RenamedFrom is not null && await roles.NameExistsAsync(name, role.Id, ct))
                throw new ConflictException(RoleRules.NameTakenMessage(name));

            saved = role with
            {
                Name = name,
                Description = description,
                StoredPermissions = permissions.Select(p => p.ToString()).ToList(),
                Version = Guid.NewGuid().ToString(),
                LastUpdated = timeProvider.GetUtcNow().UtcDateTime,
            };
            await roles.UpdateAsync(saved, request.ExpectedVersion, ct);
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.RoleChanged, request.ActorUserId, request.ActorUsername, Details: change.Details()),
                ct);
            return IdentityResult.Success;
        }, ct);

        if (change!.Any)
            await notices.SendRoleChangeToOtherAdminsAsync(request.ActorUserId, request.ActorUsername, change.Summary(), ct);

        var holders = await roles.GetHoldersAsync(saved!.Id, ct);
        return RoleMapper.ToResponse(saved, holders.Count, holders);
    }
}
