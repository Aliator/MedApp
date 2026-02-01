using MediatR;

namespace MedApp.Application.Auth.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Username,
    string Password
) : IRequest<Guid>;