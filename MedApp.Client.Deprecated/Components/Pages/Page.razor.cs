using MedApp.Client.Auth;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Pages;

public partial class Page
{
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string LoadingMessage { get; set; } = "Loading...";

    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string? Subtitle { get; set; }

    [Parameter] public bool ShowHeader { get; set; } = true;

    [Parameter] public bool ShowBackButton { get; set; }
    [Parameter] public string BackButtonTitle { get; set; } = "Back";
    [Parameter] public EventCallback OnBack { get; set; }

    [Parameter] public int? MaxWidth { get; set; }

    [Parameter] public RenderFragment? HeaderActions { get; set; }
    [Parameter] public RenderFragment? Content { get; set; }
    [Parameter] public RenderFragment? Dialogs { get; set; }
    [Parameter] public bool RequireAuth { get; set; } = true;
    [Parameter] public string LoginRedirectUrl { get; set; } = "/login";
    [Parameter] public EventCallback OnLoad { get; set; }

    private bool _isLoading = true;

    private string ContainerStyle =>
        $"max-width: {MaxWidth}px; margin: 0 auto";

    protected override async Task OnInitializedAsync()
    {
        if (RequireAuth && !await Auth.EnsureAuthenticatedAsync())
        {
            Nav.NavigateTo(LoginRedirectUrl, true);
            return;
        }

        if (OnLoad.HasDelegate)
        {
            await OnLoad.InvokeAsync();
        }

        _isLoading = false;
    }
}