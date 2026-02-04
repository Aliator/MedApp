using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Roles.Commands.AssignRole;

public sealed record AssignRoleCommand(
    string Username,
    string Role
) : IRequest<IdentityRole<Guid>?>;