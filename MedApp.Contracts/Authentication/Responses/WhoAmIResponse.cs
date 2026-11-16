namespace MedApp.Contracts.Authentication.Responses;

/// <param name="MustChangePassword">
/// The user was given a generated password and must choose their own before the API allows anything else.
/// </param>
/// <param name="MustSetUpTwoFactor">
/// The user is an admin, admins must use two-factor authentication, and they have not set it up: the API allows only
/// setting it up (after any password change) until they do.
/// </param>
public sealed record WhoAmIResponse(
    bool IsAuthenticated,
    string? Name,
    IReadOnlyList<string> Roles,
    bool MustChangePassword,
    bool MustSetUpTwoFactor = false
);
