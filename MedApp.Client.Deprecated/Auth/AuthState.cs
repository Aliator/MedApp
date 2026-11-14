using System.Net.Http.Json;
using MedApp.Contracts.Authentication.Responses;

namespace MedApp.Client.Auth;

public sealed class AuthState(HttpClient http)
{
    private bool? _isAuthenticated;
    private IReadOnlyList<string> _roles = Array.Empty<string>();
    private string _username { get; set; } = string.Empty;

    public bool IsAuthenticated => _isAuthenticated == true;
    public bool IsAdmin => _roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
    public string Username => _username;

    public async Task<bool> EnsureAuthenticatedAsync(
        CancellationToken ct = default)
    {
        if (_isAuthenticated.HasValue)
            return _isAuthenticated.Value;

        var response = await http.GetAsync(
            "api/auth/whoami",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            _isAuthenticated = false;
            _roles = Array.Empty<string>();
            return false;
        }

        var result =
            await response.Content.ReadFromJsonAsync<WhoAmIResponse>(
                cancellationToken: ct);

        _isAuthenticated = result?.IsAuthenticated ?? false;
        _roles = result?.Roles ?? Array.Empty<string>();
        _username = result?.Name ?? string.Empty;
        
        return _isAuthenticated.Value;
    }

    public void SetAuthenticated()
    {
        _isAuthenticated = null;
        _roles = Array.Empty<string>();
    }

    public void Clear()
    {
        _isAuthenticated = false;
        _roles = Array.Empty<string>();
    }
}