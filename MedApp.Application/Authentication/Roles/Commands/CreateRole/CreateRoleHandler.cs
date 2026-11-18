using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Roles.Responses;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.CreateRole;

/// <summary>
/// Makes a role. It and its <c>RoleCreated</c> activity log entry (through <see cref="ISecurityEventLog"/>: the name
/// and the permission names) are saved in one transaction, so it can't miss the log; then the other admins are emailed
/// (decision 60). Another role with the name is a 409.
/// </summary>
public sealed class CreateRoleHandler(
    IRoleRepository roles,
    IIdentityTransactions transactions,
    ISecurityEventLog events,
    ISecurityNotices notices,
    TimeProvider timeProvider)
    : IRequestHandler<CreateRoleCommand, RoleResponse>
{
    public async Task<RoleResponse> Handle(CreateRoleCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var names = RoleRules.Requested(request.Permissions).Select(p => p.ToString()).ToList();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var role = new RoleData
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = request.Description?.Trim() ?? string.Empty,
            Version = Guid.NewGuid().ToString(),
            CreatedAt = now,
            LastUpdated = now,
            StoredPermissions = names,
        };

        // A retry runs it all again, from the database as it was.
        await transactions.RunAsync(async () =>
        {
            if (await roles.NameExistsAsync(name, null, ct))
                throw new ConflictException(RoleRules.NameTakenMessage(name));

            await roles.AddAsync(role, ct);
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.RoleCreated, request.ActorUserId, request.ActorUsername,
                    Details: new Dictionary<string, object?> { ["roleId"] = role.Id, ["role"] = name, ["permissions"] = names }),
                ct);
            return IdentityResult.Success;
        }, ct);

        await notices.SendRoleChangeToOtherAdminsAsync(
            request.ActorUserId,
            request.ActorUsername,
            names.Count == 0
                ? $"created the role {name}, with no permissions beyond using the app."
                : $"created the role {name}, with {string.Join(", ", names)}.",
            ct);

        return RoleMapper.ToResponse(role, 0, []);
    }
}
