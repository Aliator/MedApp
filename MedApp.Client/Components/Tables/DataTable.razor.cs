using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MedApp.Client.Components.Tables;

public partial class DataTable : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = null!;

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
    [Parameter] public int CurrentPage { get; set; } = 1;
    [Parameter] public EventCallback<int> OnPageChange { get; set; }
    [Parameter] public EventCallback<int> OnPageSizeChanged { get; set; }

    private ElementReference _wrapperRef;
    private IJSObjectReference? _observer;
    private IJSObjectReference? _module;
    private DotNetObjectReference<DataTable>? _dotNetRef;
    private int _lastPageSize;

    private int TotalPages => _lastPageSize > 0
        ? Math.Max(1, (int)Math.Ceiling((double)TotalRows / _lastPageSize))
        : 1;

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
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./Components/Tables/DataTable.razor.js");
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
}