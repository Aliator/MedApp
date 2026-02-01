using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Commands.CreateRole;

public sealed record CreateRoleCommand(
    string Name
) : IRequest<IdentityRole<Guid>?>;