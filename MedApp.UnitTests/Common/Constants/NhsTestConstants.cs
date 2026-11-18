namespace MedApp.UnitTests.Common.Constants;

/// <summary>
/// Synthetic NHS numbers and SNOMED CT codes. The NHS numbers are all from the 999 range, which is never issued to
/// anyone real; the SNOMED CT codes are real concept IDs (terminology, not personal data).
/// </summary>
public static class NhsTestConstants
{
    /// <summary>A valid NHS number (Modulus 11 check digit 8).</summary>
    public const string NhsNumber = "9990000018";

    public const string NhsNumberWithSpaces = "999 000 0018";

    /// <summary>Another valid one.</summary>
    public const string OtherNhsNumber = "9990000026";

    /// <summary>Ten digits whose check digit is wrong (should be 8).</summary>
    public const string WrongCheckDigit = "9990000019";

    /// <summary>Ten digits whose first nine call for a check digit of 10, so no valid number starts with them.</summary>
    public const string CheckDigitTen = "9990000140";

    public const string NineDigits = "999000001";

    /// <summary>A concept: Appendectomy (pathway).</summary>
    public const string SnomedCode = "80146002";

    public const string SnomedTerm = "Appendectomy";

    /// <summary>A concept in an extension's namespace (partition 10), with a valid check digit.</summary>
    public const string ExtensionConceptCode = "100000110109";

    /// <summary>A description's ID (partition 01), with a valid check digit: not a concept.</summary>
    public const string DescriptionId = "123456017";

    /// <summary>A relationship's ID (partition 02), with a valid check digit: not a concept.</summary>
    public const string RelationshipId = "12345021";

    /// <summary>Appendectomy's code with the last digit changed: the Verhoeff check fails.</summary>
    public const string WrongVerhoeff = "80146003";
}
