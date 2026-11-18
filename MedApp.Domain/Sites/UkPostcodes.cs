using System.Text.RegularExpressions;

namespace MedApp.Domain.Sites;

/// <summary>
/// UK postcodes: their format only (an outward code, a space, an inward code: "ZZ99 3AA"), never whether one exists.
/// Accepted in any case, with or without the space, and kept upper case with one space.
/// </summary>
public static partial class UkPostcodes
{
    /// <summary>The longest a postcode is, with its space ("ZZ99 3AA").</summary>
    public const int MaxLength = 8;

    /// <summary>Whether the text is a UK postcode in format (spaces and case ignored).</summary>
    public static bool IsValid(string? text) => text is not null && Pattern().IsMatch(Compact(text));

    /// <summary>The postcode upper case with one space before its last three characters ("zz993aa" is "ZZ99 3AA").</summary>
    public static string Normalise(string text)
    {
        var compact = Compact(text);
        return compact.Length > 3 ? $"{compact[..^3]} {compact[^3..]}" : compact;
    }

    private static string Compact(string text) =>
        new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    // An outward code (A9, A99, AA9, AA99, A9A, AA9A) then an inward code (9AA).
    [GeneratedRegex("^[A-Z]{1,2}[0-9][A-Z0-9]?[0-9][A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
