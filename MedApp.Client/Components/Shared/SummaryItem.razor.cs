using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Shared;

public partial class SummaryItem
{
    [Parameter] public string Label { get; set; } = string.Empty;
    [Parameter] public string? Value { get; set; }
}