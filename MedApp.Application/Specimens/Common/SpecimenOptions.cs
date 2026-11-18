using System.Text.RegularExpressions;

namespace MedApp.Application.Specimens.Common;

/// <summary>Specimens' settings, from the <c>Specimens</c> section of the API's configuration.</summary>
public sealed partial class SpecimenOptions
{
    public const string Section = "Specimens";

    /// <summary>
    /// The letters every specimen number starts with (<c>MA26-000123</c>): one to four capital letters, so the number
    /// fits its column and can't be mistaken for another organisation's or a lab's own reference.
    /// </summary>
    public string NumberPrefix { get; set; } = "MA";

    /// <summary>Said when <see cref="NumberPrefix"/> isn't one to four capital letters, which stops the API at startup.</summary>
    public const string InvalidPrefix = "Specimens:NumberPrefix must be one to four capital letters (A to Z).";

    public bool IsValid => NumberPrefix is not null && PrefixPattern().IsMatch(NumberPrefix);

    [GeneratedRegex("^[A-Z]{1,4}$")]
    private static partial Regex PrefixPattern();
}
