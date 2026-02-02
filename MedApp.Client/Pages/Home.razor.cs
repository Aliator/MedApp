using MedApp.Client.Auth;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Pages;

public partial class Home
{
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    protected override void OnInitialized()
    {
        if (!Auth.IsAuthenticated)
            Nav.NavigateTo("/login", true);
    }
}