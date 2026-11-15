using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Roles.Commands.CreateRole;

public sealed record CreateRoleCommand(
    string Name
) : IRequest<IdentityRole<Guid>?>;