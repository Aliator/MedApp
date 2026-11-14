using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Views;

public partial class ViewSection
{
    [Parameter] public string? Title { get; set; }
    [Parameter] public bool UseGrid { get; set; } = true;
    [Parameter] public RenderFragment? ChildContent { get; set; }
}