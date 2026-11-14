namespace MedApp.Application.Common.Exceptions;

/// <summary>
/// The account is locked after too many wrong passwords, for <paramref name="MinutesLeft"/> more minutes (at least
/// one). It's refused even with the right password. The API answers it with a 423, saying how long is left.
/// </summary>
public sealed class AccountLockedOutException(int minutesLeft)
    : Exception(
        $"This account is locked after too many wrong passwords. Try again in {minutesLeft} minute{(minutesLeft == 1 ? "" : "s")}.")
{
    public int MinutesLeft { get; } = minutesLeft;
}
