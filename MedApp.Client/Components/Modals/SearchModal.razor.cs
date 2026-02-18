using MedApp.Client.Models;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Modals;

public partial class SearchModal : ComponentBase
{
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public string Title { get; set; } = "Search";
    [Parameter] public List<SearchFieldDefinition> Fields { get; set; } = [];
    [Parameter] public EventCallback OnSearch { get; set; }
    [Parameter] public EventCallback OnClear { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private async Task HandleSearch()
    {
        await OnSearch.InvokeAsync();
        await OnClose.InvokeAsync();
    }

    private async Task HandleClear()
    {
        foreach (var field in Fields)
        {
            field.Value = string.Empty;
            field.DateValue = default;
        }
        await OnClear.InvokeAsync();
        await OnClose.InvokeAsync();
    }
}