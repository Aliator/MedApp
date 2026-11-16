using MedApp.Application.Common.Identity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.CreateUser;

/// <summary>
/// Creates an account. The server generates its first password, which the result carries back once for the admin
/// to pass on. An optional <paramref name="Email"/> is sent a link to confirm it (never the password).
/// </summary>
public sealed record CreateUserCommand(
    string Username,
    string? Email = null,
    Guid? ActorUserId = null,
    string? ActorUsername = null
) : IRequest<CreateUserResult>;
