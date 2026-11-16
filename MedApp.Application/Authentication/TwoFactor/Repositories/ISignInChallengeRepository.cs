using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.TwoFactor.Repositories;

/// <summary>The sign-in challenges waiting for a second step (<see cref="SignInChallenge"/>).</summary>
public interface ISignInChallengeRepository
{
    /// <summary>
    /// Saves a new challenge. The user's earlier challenges are deleted (only the newest sign-in can finish), and so is
    /// every challenge that expired before <paramref name="utcNow"/>, so the table stays small.
    /// </summary>
    Task AddAsync(SignInChallenge challenge, DateTime utcNow, CancellationToken ct);

    /// <summary>The challenge with this token hash, or null.</summary>
    Task<SignInChallenge?> FindByTokenHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>
    /// Reserves one of the challenge's attempts before a code is checked, in one statement: it adds one to the attempts
    /// only if the challenge still exists, hasn't expired at <paramref name="utcNow"/> and has attempts left. False, with
    /// nothing changed, if it hasn't. So however many requests arrive at once, at most
    /// <see cref="Domain.Authentication.SignInChallenge.MaxFailedAttempts"/> codes are checked against one challenge.
    /// </summary>
    Task<bool> TryReserveAttemptAsync(Guid challengeId, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Uses the challenge up, by deleting it. False if it was already gone (another request used it first), so only one
    /// request can finish a sign-in.
    /// </summary>
    Task<bool> ConsumeAsync(Guid challengeId, CancellationToken ct);
}
