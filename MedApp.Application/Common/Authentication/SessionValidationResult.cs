using MedApp.Application.Common.Identity;

namespace MedApp.Application.Common.Authentication;

/// <param name="MustChangePassword">
/// The user was given a generated password and hasn't chosen their own yet, so the API refuses everything else.
/// </param>
/// <param name="MustSetUpTwoFactor">
/// The user is an admin, admins must use two-factor authentication, and they have not set it up, so the API allows only
/// setting it up.
/// </param>
/// <param name="Permissions">
/// What the user's roles allow, read with the roles on every request (null: nothing). Policies check these, never the
/// role names.
/// </param>
/// <param name="TeamIds">
/// The teams the user belongs to, read with the roles on every request (null: none): their team walls
/// (<c>TeamScope</c>).
/// </param>
public sealed record SessionValidationResult(
    Guid UserId,
    string Username,
    IReadOnlyCollection<string> Roles,
    bool MustChangePassword = false,
    bool MustSetUpTwoFactor = false,
    IReadOnlySet<Permission>? Permissions = null,
    IReadOnlyList<Guid>? TeamIds = null
);
