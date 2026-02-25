using MedApp.Application.Common.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.Login;

public sealed record LoginCommand(
    string Username,
    string Password,
    string? IpAddress,
    string? UserAgent
    ) : IRequest<LoginResult>;