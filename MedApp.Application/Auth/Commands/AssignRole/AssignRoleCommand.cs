using MediatR;

namespace MedApp.Application.Auth.Commands.AssignRole;

public sealed record AssignRoleCommand(
    string Username,
    string Role
) : IRequest;
