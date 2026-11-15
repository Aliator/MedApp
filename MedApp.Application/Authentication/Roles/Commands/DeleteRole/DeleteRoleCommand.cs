using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.DeleteRole;

public sealed record DeleteRoleCommand(
    string RoleName
) : IRequest<IdentityResult>;