namespace MedApp.Application.Letters.Common;

/// <summary>The patient's identifiers printed on every page of a letter (hazard H-50).</summary>
public static class LetterIdentifiers
{
    /// <summary>"Sam Example · Born 1 January 1970 · NHS number 999 000 0018", leaving out what isn't known.</summary>
    public static string Line(string name, string? dateOfBirth, string? nhsNumber)
    {
        var parts = new List<string> { name };
        if (!string.IsNullOrWhiteSpace(dateOfBirth))
            parts.Add($"Born {dateOfBirth}");
        if (!string.IsNullOrWhiteSpace(nhsNumber))
            parts.Add($"NHS number {nhsNumber}");
        return string.Join(" · ", parts);
    }
}
