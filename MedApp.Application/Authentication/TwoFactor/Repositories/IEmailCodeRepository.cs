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
    /// at <paramref name="utcNow"/> (not expired, and with tries left), or null.
    /// </summary>
    Task<EmailCode?> FindLiveAsync(Guid userId, EmailCodePurpose purpose, Guid? challengeId, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Saves a new code, when the account has no live one for the same purpose. Its codes for that purpose that can't
    /// work any more (expired, or out of tries) are deleted first, and so is every code that expired before
    /// <paramref name="utcNow"/>, so the table stays small. False, with nothing saved, if a live one is there (another
    /// request made it at the same moment): a filtered unique index allows one code per account and purpose outside
    /// sign-in, so two first sends at once can't both stand.
    /// </summary>
    Task<bool> AddAsync(EmailCode code, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Replaces the code with a new one (<paramref name="codeHash"/>), sent at <paramref name="utcNow"/> and working until
    /// <paramref name="expiresAtUtc"/>, in one statement: only if it's still live, has resends left, and was last sent at
    /// least <see cref="EmailCode.ResendInterval"/> ago. False, with nothing changed, otherwise, so of two resends at once
    /// only one sends.
    /// </summary>
    Task<bool> TryResendAsync(Guid codeId, string codeHash, DateTime utcNow, DateTime expiresAtUtc, CancellationToken ct);

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
