using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Shared;

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
    UserMinus,
    Home,
    Patients,
    Users,
    Logout,
    Shield,
    View,
    Edit,
    Add,
    Password,
    Assign,
    Revoke,
    Refresh,
    Search,
    Menu,
    ChevronDown,
    ChevronLeft,
    ChevronRight,
    ChevronDoubleLeft,
    ChevronDoubleRight,
    Lock,
    SortAsc,
    SortDesc
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
