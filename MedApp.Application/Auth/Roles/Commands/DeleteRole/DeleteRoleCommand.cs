using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Roles.Commands.DeleteRole;

public sealed record DeleteRoleCommand(
    string RoleName
) : IRequest<IdentityResult>;