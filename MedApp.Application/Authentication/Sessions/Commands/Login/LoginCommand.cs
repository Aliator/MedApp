using MedApp.Application.Common.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.Login;

/// <summary>
/// Signs in with a username and password: a session, or, for an account with two-factor authentication, a challenge
/// for the second step (<see cref="LoginOutcome"/>).
/// </summary>
public sealed record LoginCommand(
    string Username,
    string Password,
    string? IpAddress,
    string? UserAgent
    ) : IRequest<LoginOutcome>;
