namespace MedApp.Contracts.Authentication.Requests;

/// <summary>A new account. The server generates its first password and returns it once, in <c>CreateUserResponse</c>.</summary>
public sealed record CreateUserRequest(
    string Username);
