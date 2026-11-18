using MediatR;

namespace MedApp.Application.Authentication.Roles.Commands.DeleteRole;

/// <summary>An admin (<paramref name="ActorUserId"/>) deletes a role nobody holds.</summary>
public sealed record DeleteRoleCommand(Guid RoleId, Guid ActorUserId, string ActorUsername) : IRequest;
