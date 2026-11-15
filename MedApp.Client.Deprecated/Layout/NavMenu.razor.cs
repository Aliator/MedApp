using MedApp.Client.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MedApp.Client.Layout;

public partial class NavMenu
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    
    private bool _isMenuOpen = false;

    protected override async Task OnInitializedAsync()
    {
        await Auth.EnsureAuthenticatedAsync();
    }

    private void ToggleMenu()
    {
        _isMenuOpen = !_isMenuOpen;
    }

    private void CloseMenu()
    {
        _isMenuOpen = false;
    }

    private async Task Logout()
    {
        CloseMenu();
        await Http.PostAsync("api/auth/logout", null);
        Auth.Clear();
        Nav.NavigateTo("/login", true);
    }

    private void ViewAccount()
    {
        Nav.NavigateTo("/account");
    }
}