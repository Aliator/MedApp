using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Shared.Components;

public partial class FormGroup
{
    [Parameter] public string? Label { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public string? Hint { get; set; }
    [Parameter] public string? Error { get; set; }
    [Parameter] public string? Id { get; set; }
    [Parameter] public string CssClass { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
}