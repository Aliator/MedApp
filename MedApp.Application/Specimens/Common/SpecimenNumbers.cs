using System.Globalization;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Specimens.Common;

/// <summary>
/// Specimen numbers: MedApp's own ID for a specimen, <c>{prefix}{yy}-{nnnnnn}</c> (<c>MA26-000123</c>), from the
/// SQL sequence <c>SpecimenNumbers</c>, so two API copies never hand out the same one. The year is the UK year it was
/// recorded; the sequence never restarts, so a number is unique on its own, the year only helps people read it.
/// </summary>
public static class SpecimenNumbers
{
    /// <summary>A number from the prefix, the UK year it was recorded in and the sequence's next value.</summary>
    public static string Format(string prefix, DateTime recordedAtUtc, long sequence) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}{DueDates.ToUkDate(recordedAtUtc).Year % 100:00}-{sequence:000000}");

    /// <summary>
    /// What someone typed or scanned, as a number to look up: spaces gone and upper case, so " ma26-000123 " finds
    /// MA26-000123. Null when nothing's left, or it's longer than any number can be.
    /// </summary>
    public static string? Normalise(string? typed)
    {
        if (typed is null)
            return null;

        var number = string.Concat(typed.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        return number.Length is 0 or > Domain.Specimens.Specimen.MaxNumberLength ? null : number;
    }
}
