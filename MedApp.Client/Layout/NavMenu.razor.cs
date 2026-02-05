using MedApp.Client.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MedApp.Client.Layout;

public partial class NavMenu : IDisposable
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private bool isMenuOpen = false;
    private IJSObjectReference? module;

    protected override async Task OnInitializedAsync()
    {
        await Auth.EnsureAuthenticatedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", "./Layout/NavMenu.razor.js");
        }
    }

    private void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;
    }

    private void CloseMenu()
    {
        isMenuOpen = false;
    }

    private void CloseMenuOnMobile()
    {
        // Only close on mobile - JS will handle this
        _ = module?.InvokeVoidAsync("closeMenuOnMobile");
    }

    private async Task Logout()
    {
        CloseMenu();
        await Http.PostAsync("api/auth/logout", null);
        Auth.Clear();
        Nav.NavigateTo("/login", true);
    }

    public void Dispose()
    {
        module?.DisposeAsync();
    }
}