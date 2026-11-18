using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.TwoFactor.Repositories;

/// <summary>
/// Emailed two-factor codes (<see cref="EmailCode"/>). Everything that decides whether a code may be sent again, tried or
/// used is one SQL statement, so requests that arrive together can't send, try or use more than the rules allow.
/// </summary>
public interface IEmailCodeRepository
{
    /// <summary>
    /// The account's code for <paramref name="purpose"/> (for sign-in, the one for <paramref name="challengeId"/>), live
    /// at <paramref name="utcNow"/>, or null.
    /// </summary>
    Task<EmailCode?> FindLiveAsync(Guid userId, EmailCodePurpose purpose, Guid? challengeId, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Saves a new code. The account's earlier codes for the same purpose are deleted (a new code replaces the last), and
    /// so is every code that expired before <paramref name="utcNow"/>, so the table stays small.
    /// </summary>
    Task AddAsync(EmailCode code, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Replaces the code with a new one (<paramref name="codeHash"/>), sent at <paramref name="utcNow"/>, in one
    /// statement: only if it's still live, has resends left, and was last sent at least <see cref="EmailCode.ResendInterval"/>
    /// ago. False, with nothing changed, otherwise, so of two resends at once only one sends.
    /// </summary>
    Task<bool> TryResendAsync(Guid codeId, string codeHash, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Reserves one of the code's attempts before it's checked, in one statement: only if it's live and has attempts left
    /// (<see cref="EmailCode.MaxFailedAttempts"/>). False, with nothing changed, otherwise.
    /// </summary>
    Task<bool> TryReserveAttemptAsync(Guid codeId, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Uses the code up, by deleting it, only if it still holds <paramref name="codeHash"/> (not replaced meanwhile). False
    /// if another request used it first, so a code works once.
    /// </summary>
    Task<bool> ConsumeAsync(Guid codeId, string codeHash, CancellationToken ct);
}
