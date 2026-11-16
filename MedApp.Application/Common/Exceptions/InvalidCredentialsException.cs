namespace MedApp.Application.Common.Exceptions;

/// <summary>
/// The username or password is wrong. Deliberately the same for both, so a caller can't tell which accounts exist.
/// The API answers it with a 401.
/// </summary>
/// <param name="userId">The account's ID when the username exists (the password was wrong), for the activity log only;
/// never sent to the caller.</param>
public sealed class InvalidCredentialsException(Guid? userId = null) : Exception("Invalid credentials.")
{
    public Guid? UserId { get; } = userId;
}
