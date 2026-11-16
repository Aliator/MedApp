namespace MedApp.Domain.Terminology;

/// <summary>
/// SNOMED CT identifiers (SCTIDs), checked without a terminology server: the format only, never whether the concept
/// exists or is active. An SCTID is 6 to 18 digits with no leading zero. Its last digit is a Verhoeff check digit,
/// and the two digits before it are the partition identifier, which says what it identifies: <c>00</c> (or
/// <c>10</c>, in an extension's namespace) is a concept, <c>01</c>/<c>11</c> a description and <c>02</c>/<c>12</c>
/// a relationship. MedApp codes stages with concepts, so it takes only <c>00</c> and <c>10</c>.
/// (SNOMED International, "SNOMED CT Identifiers", in the Technical Implementation Guide.)
/// </summary>
public static class SnomedCt
{
    public const int MinLength = 6;
    public const int MaxLength = 18;

    /// <summary>The longest display term MedApp keeps (SNOMED CT's own descriptions run to 255 characters).</summary>
    public const int MaxTermLength = 255;

    /// <summary>The partition identifiers of a concept: in the international release (00), or an extension (10).</summary>
    public static readonly IReadOnlyList<string> ConceptPartitions = ["00", "10"];

    /// <summary>Why the text isn't a concept's SCTID, or null when it is.</summary>
    public static string? WhyNotConceptId(string? code)
    {
        if (string.IsNullOrEmpty(code) || !code.All(char.IsAsciiDigit))
            return "A SNOMED CT code is digits only.";
        if (code.Length is < MinLength or > MaxLength)
            return $"A SNOMED CT code is {MinLength} to {MaxLength} digits long.";
        if (code[0] == '0')
            return "A SNOMED CT code doesn't start with 0.";
        if (!HasValidCheckDigit(code))
            return "This isn't a valid SNOMED CT code: its check digit doesn't match. Check it was typed correctly.";
        if (!ConceptPartitions.Contains(Partition(code)))
            return "This SNOMED CT code identifies a description or relationship, not a concept. Use the concept ID.";
        return null;
    }

    /// <summary>Whether the text is a concept's SCTID (see <see cref="WhyNotConceptId"/>).</summary>
    public static bool IsConceptId(string? code) => WhyNotConceptId(code) is null;

    /// <summary>The partition identifier: the two digits before the check digit. Needs three digits or more.</summary>
    public static string Partition(string code) => code.Substring(code.Length - 3, 2);

    // The Verhoeff tables: the dihedral group D5's multiplication, and the permutation applied to each position.
    private static readonly int[,] Multiplication =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
        { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 },
        { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 },
        { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 },
        { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 },
        { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 },
        { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 },
        { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 },
        { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 },
        { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }
    };

    private static readonly int[,] Permutation =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
        { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 },
        { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 },
        { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 },
        { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 },
        { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 },
        { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 },
        { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 }
    };

    /// <summary>Whether the digits, check digit included, pass the Verhoeff check. Needs ASCII digits only.</summary>
    public static bool HasValidCheckDigit(string digits)
    {
        var check = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i] - '0';
            check = Multiplication[check, Permutation[i % 8, digit]];
        }

        return check == 0;
    }
}
