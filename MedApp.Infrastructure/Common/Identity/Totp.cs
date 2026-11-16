using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Time-based one-time passwords (RFC 6238), as authenticator apps make them: HMAC-SHA1 of the number of 30-second steps
/// since 1970, truncated to six digits (RFC 4226). MedApp checks codes itself, rather than with Identity's authenticator
/// token provider, so the time comes from the app's <see cref="TimeProvider"/> (testable at a fixed time) and the step a
/// code matched is known, so each code can be accepted once.
/// </summary>
public static class Totp
{
    public const int Digits = 6;

    public static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    /// <summary>Steps either side of now that are still accepted, for clocks a little out and codes typed slowly.</summary>
    public const int Window = 1;

    /// <summary>The step <paramref name="time"/> falls in.</summary>
    public static long TimeStep(DateTimeOffset time) =>
        time.ToUnixTimeSeconds() / (long)Step.TotalSeconds;

    /// <summary>The code for <paramref name="key"/> at <paramref name="timeStep"/>: six digits, with leading zeros.</summary>
    public static string Compute(byte[] key, long timeStep)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(key, counter, hash);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];

        return (binary % 1_000_000).ToString("D6");
    }

    /// <summary>
    /// The step whose code <paramref name="code"/> is, within <see cref="Window"/> steps of <paramref name="now"/>, or null
    /// if it's none of them. Spaces in the code are ignored; anything but six digits never matches. Each candidate is
    /// compared in constant time.
    /// </summary>
    public static long? Match(byte[] key, string code, DateTimeOffset now)
    {
        var digits = code.Replace(" ", string.Empty);
        if (digits.Length != Digits || !digits.All(char.IsAsciiDigit))
            return null;

        var typed = Encoding.ASCII.GetBytes(digits);
        var current = TimeStep(now);
        long? matched = null;

        for (var step = current - Window; step <= current + Window; step++)
        {
            var expected = Encoding.ASCII.GetBytes(Compute(key, step));
            if (CryptographicOperations.FixedTimeEquals(expected, typed))
                matched ??= step;
        }

        return matched;
    }
}
