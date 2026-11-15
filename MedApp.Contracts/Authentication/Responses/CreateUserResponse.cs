namespace MedApp.Contracts.Authentication.Responses;

/// <summary>
/// A new account and the one-time password the server generated for it. It's returned only this once, for the admin
/// to pass on securely.
/// </summary>
public sealed record CreateUserResponse(
    string Username,
    string Password);
