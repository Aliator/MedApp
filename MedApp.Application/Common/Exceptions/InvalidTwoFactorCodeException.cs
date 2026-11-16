namespace MedApp.Application.Common.Exceptions;

/// <summary>
/// The code given at the second step of signing in is wrong (or already used). It counts towards the lockout. The API
/// answers it with a 401, and the same sign-in can try again until its challenge runs out.
/// </summary>
public sealed class InvalidTwoFactorCodeException()
    : Exception("That code isn't right. Check it and try again, or use a recovery code.");
