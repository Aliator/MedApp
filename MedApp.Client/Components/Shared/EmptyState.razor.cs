using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Shared;

public partial class EmptyState : ComponentBase
{
    [Parameter] public IconType Icon { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public RenderFragment? Action { get; set; }
}