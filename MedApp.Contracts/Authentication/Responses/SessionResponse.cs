namespace MedApp.Contracts.Authentication.Responses;

/// <summary>The signed-in session's expiry, for the warning before it times out.</summary>
/// <param name="ExpiresAtUtc">When the session ends.</param>
/// <param name="ExpiresInSeconds">
/// How long it has left, by the server's clock. Count down from this rather than from <paramref name="ExpiresAtUtc"/>,
/// so a device whose clock is wrong still warns at the right time.
/// </param>
/// <param name="CanExtend">
/// Whether extending it would give it more time: false once it has reached the longest a session can last.
/// </param>
public sealed record SessionResponse(
    DateTime ExpiresAtUtc,
    int ExpiresInSeconds,
    bool CanExtend
);
