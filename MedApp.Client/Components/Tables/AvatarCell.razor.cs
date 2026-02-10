using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Tables;

public partial class AvatarCell : ComponentBase
{
    [Parameter] public string AvatarText { get; set; } = "";
    [Parameter] public string PrimaryText { get; set; } = "";
    [Parameter] public string? SecondaryText { get; set; }

    bool HasSecondary => !string.IsNullOrWhiteSpace(SecondaryText);
}