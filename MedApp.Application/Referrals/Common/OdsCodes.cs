namespace MedApp.Application.Referrals.Common;

/// <summary>
/// The Organisation Data Service (ODS) code of a referring practice: 3 to 10 letters and digits (a GP practice's is
/// a letter and five digits, e.g. A12345). Only the format is checked: MedApp has no link to the ODS, so it can't say
/// whether the code exists.
/// </summary>
public static class OdsCodes
{
    public const int MinLength = 3;
    public const int MaxLength = 10;

    /// <summary>The code without spaces, in upper case; null for none.</summary>
    public static string? Normalise(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        return new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
    }

    /// <summary>Whether a code (as typed) is none, or 3 to 10 ASCII letters and digits once spaces are removed.</summary>
    public static bool IsValidOrEmpty(string? code)
    {
        var normalised = Normalise(code);
        return normalised is null
               || (normalised.Length is >= MinLength and <= MaxLength && normalised.All(char.IsAsciiLetterOrDigit));
    }
}
