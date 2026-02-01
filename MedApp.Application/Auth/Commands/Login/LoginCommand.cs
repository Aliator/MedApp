using MediatR;

namespace MedApp.Application.Auth.Commands.Login;

public sealed record LoginCommand(
    string Username,
    string Password
) : IRequest<string>;