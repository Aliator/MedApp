namespace MedApp.Domain.Contacts;

/// <summary>
/// UK phone numbers, by format only: nothing checks that a number is in use. A mobile (<c>07…</c>) is stored in
/// international form (<c>+447…</c>), so a texting service can use it as it is; any UK number (a practice's) is stored
/// as typed, tidied.
/// </summary>
public static class UkPhoneNumbers
{
    /// <summary>The longest a stored mobile can be: <c>+44</c> and ten digits.</summary>
    public const int MaxMobileLength = 13;

    /// <summary>The longest a stored phone number can be, as typed.</summary>
    public const int MaxPhoneLength = 30;

    /// <summary>
    /// A UK mobile as typed (<c>07700 900123</c>, <c>+44 7700 900123</c>, <c>0044…</c>; spaces, dashes, dots and
    /// brackets ignored) in international form (<c>+447700900123</c>); null for none or anything that isn't a UK
    /// mobile.
    /// </summary>
    public static string? NormaliseMobile(string? number)
    {
        var national = NationalDigits(number);
        return national is { Length: 11 } && national.StartsWith("07", StringComparison.Ordinal)
            ? "+44" + national[1..]
            : null;
    }

    /// <summary>Whether a number, as typed, is none or a UK mobile (<c>07</c> and nine more digits).</summary>
    public static bool IsMobileOrEmpty(string? number) =>
        string.IsNullOrWhiteSpace(number) || NormaliseMobile(number) is not null;

    /// <summary>
    /// Whether a number, as typed, is none or a UK number: <c>0</c> (or <c>+44</c>) then nine or ten more digits, with
    /// spaces, dashes, dots and brackets ignored.
    /// </summary>
    public static bool IsUkNumberOrEmpty(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return true;
        var national = NationalDigits(number);
        return national is { Length: 10 or 11 } && national[0] == '0' && national[1] != '0';
    }

    /// <summary>A phone number as typed, trimmed with runs of spaces made one; null for none.</summary>
    public static string? Tidy(string? number) =>
        string.IsNullOrWhiteSpace(number)
            ? null
            : string.Join(' ', number.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// The number's digits in national form (a leading 0 in place of +44 or 0044), or null when it holds anything but
    /// digits, a leading +, spaces, dashes, dots and brackets.
    /// </summary>
    private static string? NationalDigits(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return null;

        var text = number.Trim();
        var international = text.StartsWith('+');
        if (international)
            text = text[1..];

        var digits = new List<char>();
        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c))
                digits.Add(c);
            else if (c is not (' ' or '-' or '.' or '(' or ')'))
                return null;
        }

        var all = new string(digits.ToArray());
        if (international)
            return all.StartsWith("44", StringComparison.Ordinal) ? "0" + all[2..].TrimStart('0') : null;
        if (all.StartsWith("0044", StringComparison.Ordinal))
            return "0" + all[4..].TrimStart('0');
        return all;
    }
}
