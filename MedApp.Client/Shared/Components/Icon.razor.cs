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
    UserPlus
}

public partial class Icon
{
    [Parameter] public IconType Type { get; set; }
    [Parameter] public int Size { get; set; } = 20;
}