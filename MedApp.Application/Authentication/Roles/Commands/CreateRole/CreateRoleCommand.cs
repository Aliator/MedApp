using MedApp.Contracts.Roles.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Roles.Commands.CreateRole;

/// <summary>An admin (<paramref name="ActorUserId"/>) makes a role, with the permissions it grants, by name.</summary>
public sealed record CreateRoleCommand(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions,
    Guid ActorUserId,
    string ActorUsername
) : IRequest<RoleResponse>;
