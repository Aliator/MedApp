using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Shared;

public partial class Card
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? FooterContent { get; set; }
    [Parameter] public RenderFragment? RightActions { get; set; }
    [Parameter] public bool ShowFooter { get; set; } = true;
}