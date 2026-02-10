using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Shared.Components;

public enum IconType
{
    Back,
    Email,
    Save,
    Cancel,
    Delete,
    Check,
    Alert,
    Success,
    Warning,
    Info,
    UserPlus,
    Home,
    Patients,
    Users,
    Logout,
    Shield
}

public partial class Icon
{
    [Parameter] public IconType Type { get; set; }
    [Parameter] public int Size { get; set; } = 20;
    [Parameter] public string? Class { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => Class ?? string.Empty;
}
