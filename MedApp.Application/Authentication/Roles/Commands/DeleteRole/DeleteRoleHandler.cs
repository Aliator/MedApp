using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.DeleteRole;

/// <summary>
/// Deletes a role nobody holds (decision 59); one that anyone holds is a 409 (archive it instead), and so is the
/// built-in Admin role. The delete checks again, in the same statement, that nobody holds it, so it can't take the role
/// from someone given it at that moment. Saved with its <c>RoleDeleted</c> entry (the name and the permissions it
/// granted) in one transaction; the other admins are emailed.
/// </summary>
public sealed class DeleteRoleHandler(
    IRoleRepository roles,
    IIdentityTransactions transactions,
    ISecurityEventLog events,
    ISecurityNotices notices)
    : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        RoleData? deleted = null;
        // A retry runs it all again, from the database as it was.
        await transactions.RunAsync(async () =>
        {
            var role = await roles.GetOrThrowAsync(request.RoleId, ct);
            RoleRules.EnsureEditable(role);

            if (!await roles.DeleteIfUnheldAsync(role.Id, ct))
            {
                var holders = await roles.GetHoldersAsync(role.Id, ct);
                throw new ConflictException(RoleRules.HeldMessage(role.Name, Math.Max(1, holders.Count)));
            }

            deleted = role;
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.RoleDeleted, request.ActorUserId, request.ActorUsername,
                    Details: new Dictionary<string, object?>
                    {
                        ["roleId"] = role.Id,
                        ["role"] = role.Name,
                        ["permissions"] = RoleRules.Granted(role).Select(p => p.ToString()).ToList(),
                    }),
                ct);
            return IdentityResult.Success;
        }, ct);

        await notices.SendRoleChangeToOtherAdminsAsync(
            request.ActorUserId, request.ActorUsername, $"deleted the role {deleted!.Name}, which nobody held.", ct);
    }
}
