namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The colours an activity type can have: Astryx <c>Token</c> colour names, so the theme gives them their contrast in
/// light and dark mode. Stored in lower case.
/// </summary>
public static class CalendarColours
{
    public static readonly IReadOnlyList<string> Palette =
        ["red", "orange", "yellow", "green", "teal", "cyan", "blue", "purple", "pink", "gray"];

    public static string Names => string.Join(", ", Palette);

    public static bool IsColour(string? value) =>
        value is not null && Palette.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>A palette colour as stored (lower case).</summary>
    public static string Normalise(string value) => value.Trim().ToLowerInvariant();
}
