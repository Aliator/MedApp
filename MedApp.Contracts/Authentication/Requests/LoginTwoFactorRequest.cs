namespace MedApp.Contracts.Authentication.Requests;

/// <summary>
/// The second step of signing in with two-factor authentication: the challenge the password step returned, and either
/// a code (from the authenticator app, or the one emailed for this sign-in) or one of the account's recovery codes.
/// </summary>
public sealed record LoginTwoFactorRequest(
    string Challenge,
    string? Code = null,
    string? RecoveryCode = null);
