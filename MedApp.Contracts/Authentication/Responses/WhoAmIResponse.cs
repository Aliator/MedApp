namespace MedApp.Contracts.Authentication.Responses;

/// <param name="MustChangePassword">
/// The user was given a generated password and must choose their own before the API allows anything else.
/// </param>
/// <param name="MustSetUpTwoFactor">
/// The user is an admin, admins must use two-factor authentication, and they have not set it up: the API allows only
/// setting it up (after any password change) until they do.
/// </param>
/// <param name="Permissions">
/// What the user's roles allow, by permission name (sorted), read with the session on this request: the web app shows
/// only what they can do. The server checks every request itself.
/// </param>
public sealed record WhoAmIResponse(
    bool IsAuthenticated,
    string? Name,
    IReadOnlyList<string> Roles,
    bool MustChangePassword,
    bool MustSetUpTwoFactor = false,
    IReadOnlyList<string>? Permissions = null
);
