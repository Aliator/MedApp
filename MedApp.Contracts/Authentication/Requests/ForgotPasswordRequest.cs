namespace MedApp.Contracts.Authentication.Requests;

/// <summary>Asks for a password reset link, for an account's username or its confirmed email address.</summary>
public sealed record ForgotPasswordRequest(
    string UsernameOrEmail);
