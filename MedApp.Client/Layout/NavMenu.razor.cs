using MedApp.Client.Auth;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Layout;

public partial class NavMenu
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await Auth.EnsureAuthenticatedAsync();
    }

    private async Task Logout()
    {
        await Http.PostAsync("api/auth/logout", null);
        Auth.Clear();
        Nav.NavigateTo("/login", true);
    }
}