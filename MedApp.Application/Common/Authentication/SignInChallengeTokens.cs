using System.Security.Cryptography;
using System.Text;

namespace MedApp.Application.Common.Authentication;

/// <summary>
/// The tokens of sign-in challenges (the second step of signing in with two-factor authentication): 256 random bits,
/// sent to the browser, of which only the SHA-256 hash is stored.
/// </summary>
public static class SignInChallengeTokens
{
    /// <summary>A new token: 32 random bytes, in URL-safe Base64 without padding (43 characters).</summary>
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>The hash stored for <paramref name="token"/>: SHA-256 of its UTF-8 bytes, in lower-case hex.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
