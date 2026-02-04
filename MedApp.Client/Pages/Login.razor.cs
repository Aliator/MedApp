using System.Net.Http.Json;
using MedApp.Client.Auth;
using MedApp.Contracts.Auth.Requests;
using MedApp.Contracts.Auth.Responses;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Pages;

public partial class Login
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private string _username = "";
    private string _password = "";
    private string? _error;

    private async Task HandleLogin()
    {
        _error = null;

        var response = await Http.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequest(_username, _password));

        if (!response.IsSuccessStatusCode)
        {
            _error = "Invalid credentials";
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (result is null)
        {
            _error = "Invalid response";
            return;
        }

        Auth.SetAuthenticated();

        Nav.NavigateTo("/patients");
    }
}