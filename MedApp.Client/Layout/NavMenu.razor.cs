using MedApp.Client.Auth;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Layout;

public partial class NavMenu
{
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private void Logout()
    {
        Auth.Clear();
        Nav.NavigateTo("/login");
    }
}