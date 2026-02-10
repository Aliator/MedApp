using MedApp.Client.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace MedApp.Client.Components.Navigation;

public partial class NavItem
{
    [Parameter] public string Href { get; set; } = "/";
    [Parameter] public string Text { get; set; } = string.Empty;
    [Parameter] public IconType Icon { get; set; }
    [Parameter] public NavLinkMatch Match { get; set; } = NavLinkMatch.Prefix;
}