namespace MedApp.Application.Common.Exceptions;

/// <summary>
/// The account has made too many attempts of this kind in the window, from anywhere. The API answers it with a 429 and
/// <c>Retry-After</c> (<see cref="RetryAfterSeconds"/>).
/// </summary>
public sealed class TooManyAttemptsException(string message, int retryAfterSeconds) : Exception(message)
{
    public int RetryAfterSeconds { get; } = Math.Max(1, retryAfterSeconds);
}
