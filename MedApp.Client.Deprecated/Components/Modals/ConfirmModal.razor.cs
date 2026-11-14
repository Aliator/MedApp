using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Modals;

public partial class ConfirmModal : ComponentBase
{
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string Message { get; set; } = "";

    [Parameter] public string CancelText { get; set; } = "Cancel";
    [Parameter] public string ConfirmText { get; set; } = "Confirm";
    [Parameter] public string ProcessingText { get; set; } = "Processing...";

    [Parameter] public bool IsDanger { get; set; }
    [Parameter] public bool IsProcessing { get; set; }
    [Parameter] public bool ConfirmDisabled { get; set; }

    [Parameter] public EventCallback OnConfirm { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    [Parameter] public RenderFragment? CustomIcon { get; set; }
    [Parameter] public RenderFragment? ConfirmIcon { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
}