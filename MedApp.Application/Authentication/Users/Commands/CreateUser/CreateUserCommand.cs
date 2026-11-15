using MedApp.Application.Common.Identity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.CreateUser;

/// <summary>
/// Creates an account. The server generates its first password, which the result carries back once for the admin
/// to pass on.
/// </summary>
public sealed record CreateUserCommand(
    string Username
) : IRequest<CreateUserResult>;