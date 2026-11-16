using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.AssignRole;

/// <summary>
/// Gives <paramref name="Username"/> a role. Their sessions end, except <paramref name="CurrentSessionId"/> (the one
/// making the change, if it's theirs).
/// </summary>
public sealed record AssignRoleCommand(
    string Username,
    string Role,
    Guid? CurrentSessionId = null
) : IRequest<IdentityRole<Guid>?>;
