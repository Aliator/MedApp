using System.Net.Http.Headers;

namespace MedApp.Client.Auth;

public sealed class AuthState
{
    private string? _token;

    public bool IsAuthenticated => _token is not null;
    public string? Token => _token;

    public void SetToken(string token)
    {
        _token = token;
    }

    public void Clear()
    {
        _token = null;
    }

    public void Apply(HttpClient http)
    {
        http.DefaultRequestHeaders.Authorization =
            _token is null
                ? null
                : new AuthenticationHeaderValue("Bearer", _token);
    }
}