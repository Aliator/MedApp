namespace MedApp.Contracts.Authentication.Responses;

/// <summary>
/// The password was right, and the account uses two-factor authentication: there's no session yet. Send the challenge
/// back with a code (<c>POST api/auth/login/two-factor</c>) before it expires.
/// </summary>
public sealed record TwoFactorChallengeResponse(
    string Challenge,
    DateTime ExpiresAtUtc);
