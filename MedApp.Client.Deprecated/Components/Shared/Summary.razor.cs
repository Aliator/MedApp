using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Shared;

public partial class Summary
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool IsDanger { get; set; }
}