using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Shared;

public partial class CardSection
{
    [Parameter] public string? Title { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool UseGrid { get; set; } = true;
}