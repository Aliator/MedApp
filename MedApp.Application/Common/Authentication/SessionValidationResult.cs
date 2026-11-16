namespace MedApp.Application.Common.Authentication;

/// <param name="MustChangePassword">
/// The user was given a generated password and hasn't chosen their own yet, so the API refuses everything else.
/// </param>
public sealed record SessionValidationResult(
    Guid UserId,
    string Username,
    IReadOnlyCollection<string> Roles,
    bool MustChangePassword = false
);
