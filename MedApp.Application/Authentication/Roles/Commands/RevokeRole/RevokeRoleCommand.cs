using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.RevokeRole;

/// <summary>Takes a role from a user, as the signed-in admin <paramref name="ActorUsername"/>.</summary>
public sealed record RevokeRoleCommand(
    string Username,
    string Role,
    string ActorUsername = ""
) : IRequest<IdentityRole<Guid>?>;