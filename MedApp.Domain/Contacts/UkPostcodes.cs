using System.Text.RegularExpressions;

namespace MedApp.Domain.Contacts;

/// <summary>
/// UK postcodes: the format only (an outward code such as <c>ZZ1</c> or <c>ZZ12</c>, then an inward code such as
/// <c>1ZZ</c>), stored in upper case with one space before the inward code. Nothing checks that the postcode exists.
/// </summary>
public static partial class UkPostcodes
{
    /// <summary>The longest a stored postcode can be (<c>ZZ1Z 1ZZ</c>).</summary>
    public const int MaxLength = 8;

    /// <summary>The postcode in upper case, with one space before its last three characters; null for none.</summary>
    public static string? Normalise(string? postcode)
    {
        if (string.IsNullOrWhiteSpace(postcode))
            return null;

        var compact = new string(postcode.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        return compact.Length <= 3 ? compact : $"{compact[..^3]} {compact[^3..]}";
    }

    /// <summary>Whether a postcode, as typed, is none or in a UK postcode's format (spaces anywhere, any case).</summary>
    public static bool IsValidOrEmpty(string? postcode)
    {
        var normalised = Normalise(postcode);
        return normalised is null || Format().IsMatch(normalised);
    }

    // An outward code (one or two letters, a digit, then optionally a letter or digit), a space, then a digit and two
    // letters. GIR 0AA is the one postcode outside that pattern.
    [GeneratedRegex("^(?:[A-Z]{1,2}[0-9][A-Z0-9]? [0-9][A-Z]{2}|GIR 0AA)$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
