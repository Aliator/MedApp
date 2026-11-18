namespace MedApp.Contracts.Authentication.Responses;

/// <summary>
/// The password was right, and the account uses two-factor authentication: there's no session yet. Send the challenge
/// back with a code (<c>POST api/auth/login/two-factor</c>) before it expires.
/// </summary>
/// <param name="Method">How the account gives its code: <c>Authenticator</c> (an app) or <c>Email</c>.</param>
/// <param name="EmailCode">
/// For <c>Email</c>: where the code went (masked) and when it can be sent again. Null for the app, or if no code could be
/// emailed (then only a recovery code signs in).
/// </param>
public sealed record TwoFactorChallengeResponse(
    string Challenge,
    DateTime ExpiresAtUtc,
    string Method = "Authenticator",
    EmailCodeSentResponse? EmailCode = null);
