using MedApp.Contracts.Roles.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Roles.Commands.SetRoleArchived;

/// <summary>
/// An admin (<paramref name="ActorUserId"/>) archives a role (nobody new can be given it; those who hold it keep its
/// permissions) or restores it, from the version they saw (<paramref name="ExpectedVersion"/>; 409 if changed since).
/// </summary>
public sealed record SetRoleArchivedCommand(
    Guid RoleId,
    bool IsArchived,
    string ExpectedVersion,
    Guid ActorUserId,
    string ActorUsername
) : IRequest<RoleResponse>;
