using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.Login;

/// <summary>
/// Checks the password. An account without two-factor authentication gets a session, as it always has. One with it
/// gets no session yet: a short-lived, single-use challenge (<see cref="SignInChallenge"/>), which the second step
/// (<c>LoginTwoFactorHandler</c>) exchanges, with a code, for the session.
/// </summary>
public sealed class LoginHandler(
    IAuthenticationService authenticationService,
    ISessionService sessionService,
    ITwoFactorService twoFactor,
    ISignInChallengeRepository challenges,
    TimeProvider timeProvider)
    : IRequestHandler<LoginCommand, LoginOutcome>
{
    public async Task<LoginOutcome> Handle(LoginCommand request, CancellationToken ct)
    {
        var (userId, _, _) =
            await authenticationService.ValidateCredentialsAsync(
                request.Username,
                request.Password);

        if (await twoFactor.IsEnabledAsync(userId, ct))
        {
            // Only the token's hash is stored; the browser keeps the token for the second step.
            var token = SignInChallengeTokens.Generate();
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var challenge = new SignInChallenge(userId, SignInChallengeTokens.Hash(token), now);
            await challenges.AddAsync(challenge, now, ct);

            return LoginOutcome.TwoFactorRequired(token, challenge.ExpiresAtUtc);
        }

        return LoginOutcome.SignedIn(await sessionService.CreateSessionAsync(
            userId,
            request.IpAddress,
            request.UserAgent,
            ct));
    }
}
