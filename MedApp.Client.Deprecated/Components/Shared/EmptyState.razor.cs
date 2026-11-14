using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Shared;

public partial class EmptyState : ComponentBase
{
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string Description { get; set; } = "";

    [Parameter] public string? ButtonText { get; set; }
    [Parameter] public EventCallback OnButtonClick { get; set; }

    [Parameter] public RenderFragment? CustomIcon { get; set; }
    [Parameter] public RenderFragment? ButtonIcon { get; set; }
}