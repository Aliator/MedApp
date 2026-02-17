using System.Net;
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
    
    private bool _showForgotPassword;
    private string _resetEmail = "";
    private bool _isSendingReset;
    private string? _resetError;
    private bool _resetEmailSent;
    

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
    
    private void ShowForgotPassword()
    {
        _resetError = null;
        _resetEmail = "";
        _resetEmailSent = false;
        _showForgotPassword = true;
    }

    private void HideForgotPassword()
    {
        if (_isSendingReset)
            return;

        _showForgotPassword = false;
    }

    private void CloseForgotPassword()
    {
        _showForgotPassword = false;
        _resetError = null;
        _resetEmail = "";
        _resetEmailSent = false;
    }
    
    
    private async Task RequestPasswordReset()
    {
        _resetError = null;

        if (string.IsNullOrWhiteSpace(_resetEmail))
        {
            _resetError = "Please enter your email address.";
            return;
        }

        if (!IsValidEmail(_resetEmail))
        {
            _resetError = "Please enter a valid email address.";
            return;
        }

        _isSendingReset = true;

        try
        {
            // TODO: Implement the API endpoint for password reset request
            
            await Task.Delay(1000);
            _resetEmailSent = true;

        }
        catch (HttpRequestException)
        {
            _resetError = "Unable to connect to the server. Please check your connection and try again.";
        }
        catch (Exception)
        {
            _resetError = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            _isSendingReset = false;
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
    
    private Task HandleForgotPasswordConfirm()
    {
        if (!_resetEmailSent) return RequestPasswordReset();
        CloseForgotPassword();
        return Task.CompletedTask;
    }
}