using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Tables;

public partial class DataTable : ComponentBase
{
    [Parameter] public RenderFragment? Columns { get; set; }
    [Parameter] public RenderFragment? Rows { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public int SkeletonRows { get; set; } = 8;
    [Parameter] public int SkeletonColumns { get; set; } = 4;
    [Parameter] public bool AvatarFirstColumn { get; set; }
    [Parameter] public int TableWidth { get; set; }
    [Parameter] public string[]? Widths { get; set; }
}