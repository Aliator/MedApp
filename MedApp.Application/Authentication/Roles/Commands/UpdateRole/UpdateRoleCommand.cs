using MedApp.Contracts.Roles.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Roles.Commands.UpdateRole;

/// <summary>
/// An admin (<paramref name="ActorUserId"/>) sets a role's name, description and exactly its permissions, from the
/// version they saw (<paramref name="ExpectedVersion"/>; 409 if another admin has changed it since).
/// </summary>
public sealed record UpdateRoleCommand(
    Guid RoleId,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions,
    string ExpectedVersion,
    Guid ActorUserId,
    string ActorUsername
) : IRequest<RoleResponse>;
