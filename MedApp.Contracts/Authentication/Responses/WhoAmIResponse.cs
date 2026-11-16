namespace MedApp.Contracts.Authentication.Responses;

/// <param name="MustChangePassword">
/// The user was given a generated password and must choose their own before the API allows anything else.
/// </param>
public sealed record WhoAmIResponse(
    bool IsAuthenticated,
    string? Name,
    IReadOnlyList<string> Roles,
    bool MustChangePassword
);
