using MedApp.Client.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Tables;

public partial class ActionButton : ComponentBase
{
    [Parameter] public IconType Icon { get; set; }
    [Parameter] public string? Text { get; set; }
    [Parameter] public string? Title { get; set; }
    [Parameter] public string CssClass { get; set; } = "";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
}