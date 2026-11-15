namespace MedApp.Application.Common.Exceptions;

/// <summary>
/// The username or password is wrong. Deliberately the same for both, so a caller can't tell which accounts exist.
/// The API answers it with a 401.
/// </summary>
public sealed class InvalidCredentialsException() : Exception("Invalid credentials.");
