using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Views;

public partial class ViewGroup
{
    [Parameter] public string Label { get; set; } = string.Empty;
    [Parameter] public string CssClass { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
}