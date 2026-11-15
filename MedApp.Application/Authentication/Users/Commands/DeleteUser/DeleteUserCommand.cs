using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.DeleteUser;

/// <summary>
/// Deletes a user, once the signed-in admin (<paramref name="ActorUsername"/>) has confirmed with their own
/// password (<paramref name="ActorPassword"/>).
/// </summary>
public sealed record DeleteUserCommand(
    string Username,
    string ActorUsername,
    string ActorPassword
) : IRequest<IdentityResult>;