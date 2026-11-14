using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Modals;

public partial class SelectionModal<TItem> : ComponentBase
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

    [Parameter] public IReadOnlyList<TItem> Items { get; set; } = Array.Empty<TItem>();

    [Parameter] public Func<TItem, string>? LabelSelector { get; set; }
    [Parameter] public Func<TItem, bool>? SelectedPredicate { get; set; }
    [Parameter] public EventCallback<SelectionToggle<TItem>> Toggle { get; set; }

    [Parameter] public RenderFragment? CustomIcon { get; set; }
    [Parameter] public RenderFragment? ConfirmIcon { get; set; }
    [Parameter] public RenderFragment? InfoContent { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }

    private string GetLabel(TItem item) => LabelSelector?.Invoke(item) ?? item?.ToString() ?? "";
    private bool IsSelected(TItem item) => SelectedPredicate?.Invoke(item) ?? false;

    private Task OnToggle(TItem item, bool isSelected) =>
        Toggle.InvokeAsync(new SelectionToggle<TItem>(item, isSelected));
}