using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Pages;

public partial class PageHeader
{
    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string? Subtitle { get; set; }
    [Parameter] public bool ShowBackButton { get; set; } = true;
    [Parameter] public string BackButtonTitle { get; set; } = "Back";
    [Parameter] public EventCallback OnBack { get; set; }
    [Parameter] public RenderFragment? Actions { get; set; }
}