using MediatR;

namespace MedApp.Application.Auth.Commands.CreateRole;

public sealed record CreateRoleCommand(
    string Name
) : IRequest;