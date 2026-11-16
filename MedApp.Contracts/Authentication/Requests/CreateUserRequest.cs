namespace MedApp.Contracts.Authentication.Requests;

/// <summary>A new account. The server generates its first password and returns it once, in <c>CreateUserResponse</c>.</summary>
/// <param name="Email">
/// Optional: an address for the account, sent a link to confirm it, and used once that's opened. The password is never
/// emailed.
/// </param>
public sealed record CreateUserRequest(
    string Username,
    string? Email = null);
