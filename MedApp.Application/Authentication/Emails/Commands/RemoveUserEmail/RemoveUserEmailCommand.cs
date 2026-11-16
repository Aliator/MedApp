using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail;

/// <summary>
/// An admin (<paramref name="ActorUserId"/>) removes another account's email address and any pending one, for example
/// after an address was put on the account without its owner (so "Forgot your password?" can't hand it back).
/// </summary>
public sealed record RemoveUserEmailCommand(
    string Username,
    Guid ActorUserId,
    string ActorUsername
) : IRequest<IdentityResult>;
