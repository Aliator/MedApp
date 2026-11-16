namespace MedApp.Application.Common.Authentication;

/// <param name="MustChangePassword">
/// The user was given a generated password and hasn't chosen their own yet, so the API refuses everything else.
/// </param>
/// <param name="MustSetUpTwoFactor">
/// The user is an admin, admins must use two-factor authentication, and they have not set it up, so the API allows only
/// setting it up.
/// </param>
public sealed record SessionValidationResult(
    Guid UserId,
    string Username,
    IReadOnlyCollection<string> Roles,
    bool MustChangePassword = false,
    bool MustSetUpTwoFactor = false
);
