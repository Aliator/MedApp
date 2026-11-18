namespace MedApp.Contracts.Authentication.Responses;

/// <param name="Email">The account's confirmed email address; null if none.</param>
/// <param name="PendingEmail">An address waiting for its confirmation link to be opened; null if none.</param>
/// <param name="TwoFactorEnabled">Whether the account signs in with an authenticator code as well as its password.</param>
/// <param name="FullName">The person's full name as it goes on letters; null until given.</param>
/// <param name="JobTitle">Their job title as it goes on letters; null until given.</param>
public sealed record UserResponse(
    Guid UserId,
    string Username,
    IReadOnlyList<string> Roles,
    string? Email = null,
    string? PendingEmail = null,
    bool TwoFactorEnabled = false,
    string? FullName = null,
    string? JobTitle = null
);