using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.RevokeRole;

/// <summary>
/// Takes a role from <paramref name="Username"/>. Their sessions end, except <paramref name="CurrentSessionId"/> (the
/// one making the change, if it's theirs).
/// </summary>
public sealed record RevokeRoleCommand(
    string Username,
    string Role,
    Guid? CurrentSessionId = null
) : IRequest<IdentityRole<Guid>?>;
