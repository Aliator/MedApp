using System.Text;

namespace MedApp.Application.Common.Identity;

/// <summary>
/// How an authenticator app is given an account's key: an <c>otpauth://</c> address (Google's Key Uri Format, which
/// every common app reads from a QR code), or the key typed in, shown in groups of four.
/// </summary>
public static class AuthenticatorUri
{
    /// <summary>The name apps show beside the code.</summary>
    public const string Issuer = "MedApp";

    /// <summary>The address for <paramref name="username"/>'s key: TOTP, six digits, 30-second steps, SHA-1.</summary>
    public static string Build(string username, string key)
    {
        var label = Uri.EscapeDataString($"{Issuer}:{username}");
        return $"otpauth://totp/{label}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6&period=30";
    }

    /// <summary>The key in lower-case groups of four, easier to read and type ("abcd efgh ...").</summary>
    public static string FormatKey(string key)
    {
        var result = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (result.Length > 0)
                result.Append(' ');
            result.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }

        return result.ToString().ToLowerInvariant();
    }
}
