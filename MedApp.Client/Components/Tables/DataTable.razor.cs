using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace MedApp.Client.Components.Tables;

public partial class DataTable : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = null!;

    [Parameter] public RenderFragment? Columns { get; set; }
    [Parameter] public RenderFragment? Rows { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public int SkeletonRows { get; set; } = 8;
    [Parameter] public int SkeletonColumns { get; set; } = 4;
    [Parameter] public bool AvatarFirstColumn { get; set; }
    [Parameter] public int TableWidth { get; set; }
    [Parameter] public string[]? Widths { get; set; }
    [Parameter] public string? SortColumn { get; set; }
    [Parameter] public bool SortAscending { get; set; } = true;
    [Parameter] public EventCallback<(string Column, bool Ascending)> OnSort { get; set; }
    [Parameter] public int TotalRows { get; set; }
    [Parameter] public int TotalPages { get; set; } = 1;
    [Parameter] public int CurrentPage { get; set; } = 1;
    [Parameter] public EventCallback<int> OnPageChange { get; set; }
    [Parameter] public EventCallback<int> OnPageSizeChanged { get; set; }

    private ElementReference _wrapperRef;
    private IJSObjectReference? _observer;
    private IJSObjectReference? _module;
    private DotNetObjectReference<DataTable>? _dotNetRef;
    private int _lastPageSize;

    private bool _editingPage;
    private int _pageInputValue;
    private ElementReference _pageInputRef;

    private async Task StartEditingPage()
    {
        _pageInputValue = CurrentPage;
        _editingPage = true;
        await Task.Yield();
        await Js.InvokeVoidAsync("eval", "document.querySelector('.pagination-page-input')?.focus()");
    }

    private async Task CommitPageInput()
    {
        _editingPage = false;
        var clamped = Math.Clamp(_pageInputValue, 1, TotalPages);
        if (clamped != CurrentPage)
            await OnPageChange.InvokeAsync(clamped);
    }

    private async Task HandlePageInputKey(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await CommitPageInput();
        else if (e.Key == "Escape") _editingPage = false;
    }

    [JSInvokable]
    public async Task UpdatePageSize(int pageSize)
    {
        if (pageSize == _lastPageSize) return;
        _lastPageSize = pageSize;
        await OnPageSizeChanged.InvokeAsync(pageSize);
        StateHasChanged();
    }

    private async Task PrevPage()
    {
        if (CurrentPage > 1)
            await OnPageChange.InvokeAsync(CurrentPage - 1);
    }

    private async Task NextPage()
    {
        if (CurrentPage < TotalPages)
            await OnPageChange.InvokeAsync(CurrentPage + 1);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _module = await Js.InvokeAsync<IJSObjectReference>("import", "./Components/Tables/DataTable.razor.js");
            _observer = await _module.InvokeAsync<IJSObjectReference>("measureAndObserve", _wrapperRef, _dotNetRef);
        }
        else if (_observer != null && !IsLoading)
        {
            await _observer.InvokeVoidAsync("recalculate");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_observer != null)
        {
            await _observer.InvokeVoidAsync("dispose");
            await _observer.DisposeAsync();
        }

        if (_module != null)
            await _module.DisposeAsync();

        _dotNetRef?.Dispose();
    }
    
    private async Task FirstPage()
    {
        if (CurrentPage > 1)
            await OnPageChange.InvokeAsync(1);
    }

    private async Task LastPage()
    {
        if (CurrentPage < TotalPages)
            await OnPageChange.InvokeAsync(TotalPages);
    }
}