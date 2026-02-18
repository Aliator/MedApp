using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Modals;

public partial class UserSearchModal : ComponentBase
{
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public UserSearchCriteria Criteria { get; set; } = new(string.Empty, string.Empty);
    [Parameter] public EventCallback<UserSearchCriteria> OnSearch { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private ElementReference _firstInputRef;

    private string _username = string.Empty;
    private string _role = string.Empty;

    protected override async Task OnParametersSetAsync()
    {
        if (IsVisible)
        {
            _username = Criteria.Username ?? string.Empty;
            _role = Criteria.Role ?? string.Empty;

            await Task.Yield();
            try { await _firstInputRef.FocusAsync(); } catch { }
        }
    }

    private async Task Apply()
    {
        await OnSearch.InvokeAsync(new UserSearchCriteria(_username, _role));
        await OnClose.InvokeAsync();
    }

    private async Task Clear()
    {
        _username = string.Empty;
        _role = string.Empty;

        await OnSearch.InvokeAsync(new UserSearchCriteria(string.Empty, string.Empty));
        await OnClose.InvokeAsync();
    }
}

public record UserSearchCriteria(string Username, string Role);