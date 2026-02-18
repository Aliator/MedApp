using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Tables;

public partial class AvatarCell : ComponentBase
{
    [Parameter] public string AvatarText { get; set; } = "";
    [Parameter] public string PrimaryText { get; set; } = "";
    [Parameter] public string? SecondaryText { get; set; }

    private bool HasSecondary => !string.IsNullOrWhiteSpace(SecondaryText);

    private bool _isNameTruncated;
    private string _displayPrimaryText = "";

    private string DisplayPrimaryText => _displayPrimaryText;
    private bool ShowHoverFullName => HasSecondary && _isNameTruncated;

    protected override void OnParametersSet()
    {
        (_displayPrimaryText, _isNameTruncated) = ShortenPatientName(PrimaryText);
    }

    private static (string Display, bool IsTruncated) ShortenPatientName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (value, false);

        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return (value, false);

        var truncated = false;

        if (parts.Length == 1)
        {
            var p = TruncatePart(parts[0], ref truncated);
            return (p, truncated);
        }

        TruncatePartInPlace(parts, 0, ref truncated);
        TruncatePartInPlace(parts, parts.Length - 1, ref truncated);

        return (string.Join(' ', parts), truncated);
    }

    private static void TruncatePartInPlace(string[] parts, int index, ref bool truncated)
    {
        parts[index] = TruncatePart(parts[index], ref truncated);
    }

    private static string TruncatePart(string part, ref bool truncated)
    {
        if (string.IsNullOrWhiteSpace(part))
            return part;

        if (part.Length > 9)
        {
            truncated = true;
            return part[..8] + "..";
        }

        return part;
    }
}