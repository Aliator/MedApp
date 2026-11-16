namespace MedApp.Application.Common.Exceptions;

/// <summary>
/// The second step of signing in can't go on: its challenge is unknown, has expired, was already used, or has had too
/// many wrong codes. The API answers it with a 401 whose <c>code</c> is <see cref="Code"/>, so the sign-in page starts
/// again from the password.
/// </summary>
public sealed class SignInExpiredException()
    : Exception("Your sign-in has timed out. Sign in again.")
{
    public const string Code = "sign_in_expired";
}
