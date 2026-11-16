using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.TwoFactor.Commands.ResetUserTwoFactor;

/// <summary>
/// An admin (<paramref name="ActorUserId"/>) turns off <paramref name="Username"/>'s two-factor authentication, for
/// someone who has lost their app and their recovery codes, so they can sign in with their password and set it up again.
/// Every session the user has ends. Admins can't reset their own.
/// </summary>
public sealed record ResetUserTwoFactorCommand(
    string Username,
    Guid ActorUserId,
    string ActorUsername
) : IRequest<IdentityResult>;
