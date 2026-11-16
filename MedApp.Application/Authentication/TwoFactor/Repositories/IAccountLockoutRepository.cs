namespace MedApp.Application.Authentication.TwoFactor.Repositories;

/// <summary>
/// Counts wrong passwords and two-factor codes towards an account's lockout in single SQL statements, so requests that
/// arrive together can't lose each other's counts (Identity's own <c>AccessFailedAsync</c> reads, then writes, and loses
/// all but one of them to its concurrency check, leaving the failed update to break the request's next save).
/// </summary>
public interface IAccountLockoutRepository
{
    /// <summary>
    /// Adds one wrong code to the account's count. When that reaches <paramref name="maxFailures"/>, the account is
    /// locked (if lockout is on for it) for <see cref="CodeLockouts.Duration"/> of its earlier code lockouts, its count
    /// set back to 0 and its code lockouts counted, all in one statement, so only one request applies it. Returns the
    /// lockout's length if this call applied it, otherwise null.
    /// </summary>
    Task<TimeSpan?> RecordFailedCodeAsync(
        Guid userId,
        int maxFailures,
        TimeSpan baseLockout,
        DateTimeOffset now,
        CancellationToken ct);

    /// <summary>
    /// Adds one wrong password to the account's count; at <paramref name="maxFailures"/> the account is locked for
    /// <paramref name="lockout"/> and its count set back to 0, in one statement, as Identity's lockout does. Returns the
    /// lockout's length if this call applied it, otherwise null.
    /// </summary>
    Task<TimeSpan?> RecordFailedPasswordAsync(
        Guid userId,
        int maxFailures,
        TimeSpan lockout,
        DateTimeOffset now,
        CancellationToken ct);

    /// <summary>After a complete sign-in: no failures counted, and no code lockouts to grow from.</summary>
    Task ResetAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Stops tracking the account (and its tokens) in this request, after a failed or bypassed Identity update, so a later
    /// save in the same request (the activity log's) doesn't send that update again and fail.
    /// </summary>
    void Forget(Guid userId);
}
