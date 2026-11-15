using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Forms;

public partial class FormCard
{
    [Parameter] public RenderFragment? Sections { get; set; }
    [Parameter] public RenderFragment? FooterActions { get; set; }
    [Parameter] public bool ShowFooter { get; set; } = true;
    [Parameter] public EventCallback OnSave { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public EventCallback OnDelete { get; set; }
    [Parameter] public bool IsProcessing { get; set; }
    [Parameter] public string SaveText { get; set; } = "Save";
    [Parameter] public string CancelText { get; set; } = "Cancel";
    [Parameter] public string DeleteText { get; set; } = "Delete";
    [Parameter] public string ProcessingText { get; set; } = "Saving...";
}