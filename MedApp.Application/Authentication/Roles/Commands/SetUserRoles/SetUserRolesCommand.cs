using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.SetUserRoles;

/// <summary>Sets a user's roles to exactly <paramref name="Roles"/>, as the signed-in admin <paramref name="ActorUsername"/>.</summary>
public sealed record SetUserRolesCommand(
    string Username,
    IReadOnlyList<string> Roles,
    string ActorUsername
) : IRequest<IdentityResult>;
