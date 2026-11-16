namespace MedApp.Application.Common.Identity;

/// <summary>What a username sent to sign in can be, before anything looks it up.</summary>
public static class UsernameRules
{
    /// <summary>The longest an account's name can be (Identity's column). Sign-in refuses anything longer (a 400).</summary>
    public const int MaxLength = 256;

    /// <summary>
    /// Whether <paramref name="username"/> could be an account's name: no longer than <see cref="MaxLength"/>, and made
    /// only of <paramref name="allowedCharacters"/> (Identity's <c>User.AllowedUserNameCharacters</c>, which every
    /// account was created under; empty or null allows any). Trailing spaces are ignored, as SQL Server ignores them
    /// when it compares names, so a name typed with one still signs in as before. A name that couldn't be an account's is
    /// treated as unknown without looking it up: SQL Server would otherwise match some of them to a real account (a
    /// byte order mark in front, a NUL after it, full-width letters), so each spelling would get a sign-in allowance of
    /// its own.
    /// </summary>
    public static bool CouldExist(string? username, string? allowedCharacters)
    {
        if (string.IsNullOrEmpty(username) || username.Length > MaxLength)
            return false;

        var name = username.TrimEnd(' ');
        if (name.Length == 0)
            return false;

        return string.IsNullOrEmpty(allowedCharacters) || name.All(allowedCharacters.Contains);
    }
}
