using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Tables;

public partial class SortableHeader : ComponentBase
{
    [CascadingParameter(Name = "SortColumn")] public string? CurrentSortColumn { get; set; }
    [CascadingParameter(Name = "SortAscending")] public bool CurrentSortAscending { get; set; }
    [CascadingParameter(Name = "OnSort")] public EventCallback<(string Column, bool Ascending)> OnSort { get; set; }

    [Parameter, EditorRequired] public string Column { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private bool IsActive => CurrentSortColumn == Column;

    private async Task HandleClick()
    {
        var ascending = IsActive ? !CurrentSortAscending : true;
        await OnSort.InvokeAsync((Column, ascending));
    }
}