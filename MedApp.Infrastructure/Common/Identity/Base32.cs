namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Base32 (RFC 4648, the alphabet A to Z then 2 to 7), the form authenticator keys take: Identity stores them so, and
/// apps read them so.
/// </summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>The bytes <paramref name="text"/> encodes, ignoring case, spaces and padding; throws for any other character.</summary>
    public static byte[] Decode(string text)
    {
        var clean = text.Replace(" ", string.Empty).TrimEnd('=').ToUpperInvariant();
        var bytes = new byte[clean.Length * 5 / 8];

        var buffer = 0;
        var bits = 0;
        var index = 0;
        foreach (var c in clean)
        {
            var value = Alphabet.IndexOf(c);
            if (value < 0)
                throw new FormatException($"'{c}' isn't a Base32 character.");

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes[index++] = (byte)(buffer >> bits);
                buffer &= (1 << bits) - 1;
            }
        }

        return bytes;
    }

    /// <summary><paramref name="bytes"/> in Base32, upper case, without padding.</summary>
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        var result = new System.Text.StringBuilder((bytes.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                result.Append(Alphabet[(buffer >> bits) & 31]);
            }

            buffer &= (1 << bits) - 1;
        }

        if (bits > 0)
            result.Append(Alphabet[(buffer << (5 - bits)) & 31]);

        return result.ToString();
    }
}
