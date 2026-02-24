using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.RevokeRole;

public sealed record RevokeRoleCommand(
    string Username,
    string Role
) : IRequest<IdentityRole<Guid>?>;