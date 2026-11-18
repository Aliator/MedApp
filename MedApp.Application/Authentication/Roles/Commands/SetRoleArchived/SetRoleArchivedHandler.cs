using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Roles.Responses;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.SetRoleArchived;

/// <summary>
/// Archives or restores a role (decision 59). Archiving takes nothing from anyone: it only hides the role from the
/// picker. The built-in Admin role refuses (409). Saved with its <c>RoleChanged</c> entry in one transaction, from the
/// version the admin saw; the other admins are emailed. Asking for what it already is changes and records nothing.
/// </summary>
public sealed class SetRoleArchivedHandler(
    IRoleRepository roles,
    IIdentityTransactions transactions,
    ISecurityEventLog events,
    ISecurityNotices notices,
    TimeProvider timeProvider)
    : IRequestHandler<SetRoleArchivedCommand, RoleResponse>
{
    public async Task<RoleResponse> Handle(SetRoleArchivedCommand request, CancellationToken ct)
    {
        RoleData? saved = null;
        RoleChange? change = null;
        // A retry runs it all again, from the database as it was.
        await transactions.RunAsync(async () =>
        {
            var role = await roles.GetOrThrowAsync(request.RoleId, ct);
            RoleRules.EnsureEditable(role);
            RoleRules.EnsureVersion(role, request.ExpectedVersion);

            saved = role;
            change = null;
            if (role.IsArchived == request.IsArchived)
                return IdentityResult.Success;

            change = RoleChange.Archiving(role, request.IsArchived);
            saved = role with
            {
                IsArchived = request.IsArchived,
                Version = Guid.NewGuid().ToString(),
                LastUpdated = timeProvider.GetUtcNow().UtcDateTime,
            };
            await roles.UpdateAsync(saved, request.ExpectedVersion, ct);
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.RoleChanged, request.ActorUserId, request.ActorUsername, Details: change.Details()),
                ct);
            return IdentityResult.Success;
        }, ct);

        if (change is not null)
            await notices.SendRoleChangeToOtherAdminsAsync(request.ActorUserId, request.ActorUsername, change.Summary(), ct);

        var holders = await roles.GetHoldersAsync(saved!.Id, ct);
        return RoleMapper.ToResponse(saved, holders.Count, holders);
    }
}
