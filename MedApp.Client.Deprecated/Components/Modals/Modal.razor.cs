using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Modals;

public partial class Modal : ComponentBase
{
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public bool IsDanger { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    [Parameter] public bool CloseOnBackdropClick { get; set; } = true;

    [Parameter] public RenderFragment? Body { get; set; }
    [Parameter] public RenderFragment? Footer { get; set; }

    private Task HandleOverlayClick()
        => CloseOnBackdropClick ? OnClose.InvokeAsync() : Task.CompletedTask;
}