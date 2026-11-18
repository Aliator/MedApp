namespace MedApp.Contracts.Authentication.Responses;

/// <summary>A two-factor code was emailed. Never the code, and never the whole address.</summary>
/// <param name="SentTo">The address it went to, masked (<c>j•••@example.com</c>), so a password alone doesn't reveal it.</param>
/// <param name="ExpiresAtUtc">When the code stops working.</param>
/// <param name="ResendInSeconds">Seconds before it can be sent again, by the server's clock (0: now, if any are left).</param>
/// <param name="ResendsLeft">How many more times it can be sent.</param>
public sealed record EmailCodeSentResponse(
    string SentTo,
    DateTime ExpiresAtUtc,
    int ResendInSeconds,
    int ResendsLeft);
