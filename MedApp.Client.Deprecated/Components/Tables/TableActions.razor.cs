using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Tables;

public partial class TableActions : ComponentBase
{
    [Parameter] public RenderFragment ChildContent { get; set; } = default!;
}