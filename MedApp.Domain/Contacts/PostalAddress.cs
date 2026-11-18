namespace MedApp.Domain.Contacts;

/// <summary>
/// A postal address's lines (house and street, locality, town: at most <see cref="MaxLines"/>, each at most
/// <see cref="MaxLineLength"/> characters), stored as one column with a line feed between lines. The postcode is a
/// column of its own (<see cref="UkPostcodes"/>).
/// </summary>
public static class PostalAddress
{
    public const int MaxLines = 3;
    public const int MaxLineLength = 100;

    /// <summary>The longest the stored column can be: every line at its longest, with a separator between each.</summary>
    public const int MaxStoredLength = MaxLines * MaxLineLength + (MaxLines - 1);

    private const char Separator = '\n';

    /// <summary>The lines, trimmed, without empty ones, as one value to store; null when there are none.</summary>
    public static string? Join(IEnumerable<string?>? lines)
    {
        var kept = Tidy(lines);
        return kept.Count == 0 ? null : string.Join(Separator, kept);
    }

    /// <summary>A stored value's lines; none for null.</summary>
    public static IReadOnlyList<string> Split(string? stored) =>
        string.IsNullOrEmpty(stored) ? [] : stored.Split(Separator);

    /// <summary>The lines trimmed, with empty ones (and line breaks inside one) dropped.</summary>
    public static IReadOnlyList<string> Tidy(IEnumerable<string?>? lines) =>
        (lines ?? [])
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => string.Join(' ', line!.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim())
        .Where(line => line.Length > 0)
        .ToList();
}
