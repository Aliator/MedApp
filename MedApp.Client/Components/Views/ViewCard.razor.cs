using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Views;

public partial class ViewCard
{
    [Parameter] public RenderFragment? Sections { get; set; }
    [Parameter] public RenderFragment? FooterActions { get; set; }
    [Parameter] public bool ShowFooter { get; set; } = true;
}