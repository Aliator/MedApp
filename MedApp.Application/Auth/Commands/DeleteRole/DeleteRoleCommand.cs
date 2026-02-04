using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Commands.DeleteRole;

public sealed record DeleteRoleCommand(
    string RoleName
) : IRequest<IdentityResult>;