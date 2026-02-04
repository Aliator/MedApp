namespace MedApp.Client.Auth;

public sealed class AuthState
{
    public bool IsAuthenticated { get; private set; }

    public void SetAuthenticated()
    {
        IsAuthenticated = true;
    }

    public void Clear()
    {
        IsAuthenticated = false;
    }
}