using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Roles.Commands.RevokeRole;

public sealed record RevokeRoleCommand(
    string Username,
    string Role
) : IRequest<IdentityRole<Guid>?>;