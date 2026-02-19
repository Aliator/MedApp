using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Tables;

public partial class AvatarCell : ComponentBase
{
    [Parameter] public string AvatarText { get; set; } = "";
    [Parameter] public string PrimaryText { get; set; } = "";
    [Parameter] public string? SecondaryText { get; set; }

    [Parameter] public bool EnableNameTruncation { get; set; } = true;

    private bool HasSecondary => !string.IsNullOrWhiteSpace(SecondaryText);

    private bool _isNameTruncated;
    private string _displayPrimaryText = "";

    private string DisplayPrimaryText => _displayPrimaryText;
    private bool ShowHoverFullName => HasSecondary && EnableNameTruncation && _isNameTruncated;

    protected override void OnParametersSet()
    {
        if (!EnableNameTruncation)
        {
            _displayPrimaryText = PrimaryText;
            _isNameTruncated = false;
            return;
        }

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

        parts[0] = TruncatePart(parts[0], ref truncated);
        parts[^1] = TruncatePart(parts[^1], ref truncated);

        return (string.Join(' ', parts), truncated);
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