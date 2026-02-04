using System.Net.Http.Json;
using MedApp.Contracts.Auth.Responses;

namespace MedApp.Client.Auth;

public sealed class AuthState(HttpClient http)
{
    private bool? _isAuthenticated;

    public bool IsAuthenticated => _isAuthenticated == true;

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
            return false;
        }

        var result =
            await response.Content.ReadFromJsonAsync<WhoAmIResponse>(
                cancellationToken: ct);

        _isAuthenticated = result?.IsAuthenticated ?? false;
        return _isAuthenticated.Value;
    }

    public void SetAuthenticated()
    {
        _isAuthenticated = true;
    }

    public void Clear()
    {
        _isAuthenticated = false;
    }
}