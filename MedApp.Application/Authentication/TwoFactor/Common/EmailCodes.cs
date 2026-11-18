using System.Security.Cryptography;
using System.Text;
using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.TwoFactor.Common;

/// <summary>
/// Making and checking emailed two-factor codes: 6 digits from a cryptographic random number generator, stored only as
/// a SHA-256 hash bound to the code's own row (so equal codes for two people hash differently), and compared in constant
/// time. Six digits are few enough to be worked out from a copy of the hash, so the hash keeps codes out of sight (the
/// database, logs, backups), not safe from a copy: their short life, single use and attempt limits protect them.
/// </summary>
public static class EmailCodes
{
    /// <summary>How many digits a code has.</summary>
    public const int Length = 6;

    /// <summary>A new code: 6 digits, each equally likely, leading zeros kept.</summary>
    public static string Generate() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A code as typed, cleaned as every check cleans it (<see cref="TwoFactorCodes.Normalise"/>).</summary>
    public static string Normalise(string? code) => TwoFactorCodes.Normalise(code);

    /// <summary>Whether <paramref name="code"/> (spaces ignored) has the shape of a code: exactly 6 ASCII digits.</summary>
    public static bool IsWellFormed(string? code)
    {
        var digits = Normalise(code);
        return digits.Length == Length && digits.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// The hash stored for <paramref name="code"/> on the row <paramref name="codeId"/>: SHA-256, lower-case hex. With
    /// <paramref name="boundTo"/> (the address a code for turning codes on was sent to), the code works only while that's
    /// still the account's confirmed address.
    /// </summary>
    public static string Hash(Guid codeId, string code, string? boundTo = null) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            boundTo is null
                ? $"{codeId:N}:{Normalise(code)}"
                : $"{codeId:N}:{boundTo.Trim().ToLowerInvariant()}:{Normalise(code)}")));

    /// <summary>Whether <paramref name="code"/> is the one <paramref name="stored"/> holds, compared in constant time.</summary>
    public static bool Matches(EmailCode stored, string code, string? boundTo = null) =>
        IsWellFormed(code)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(stored.Id, code, boundTo)),
            Encoding.ASCII.GetBytes(stored.CodeHash));
}
