using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedApp.Client.Auth;
using MedApp.Contracts.Auth.Requests;
using MedApp.Contracts.Common;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Pages;

public partial class Account
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;

    private string _currentPassword = "";
    private string _newPassword = "";
    private string _confirmNewPassword = "";

    private bool _isChanging;
    private string? _error;
    private string? _successMessage;
    
    private List<string> _validationErrors = [];
    
    private async Task ChangePassword()
    {
        _error = null;
        _successMessage = null;
        _validationErrors.Clear();

        if (_newPassword != _confirmNewPassword)
        {
            _error = "New passwords do not match.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentPassword))
        {
            _error = "Please enter your current password.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_newPassword))
        {
            _error = "Please enter a new password.";
            return;
        }

        _isChanging = true;

        try
        {
            var response = await Http.PutAsJsonAsync(
                $"api/auth/users/{Auth.Username}/update-password",
                new UpdateUserPasswordRequest(
                    _currentPassword,
                    _newPassword));

            if (response.IsSuccessStatusCode)
            {
                _successMessage = "Password updated successfully!";
                _currentPassword = "";
                _newPassword = "";
                _confirmNewPassword = "";
                return;
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                await HandlePasswordValidationErrorsAsync(response);
                return;
            }

            _error = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Current password is incorrect.",
                HttpStatusCode.Forbidden => "You do not have permission to update this password.",
                HttpStatusCode.NotFound => "User not found.",
                HttpStatusCode.InternalServerError => "A server error occurred. Please try again later.",
                _ => "An unexpected error occurred while updating the password."
            };
        }
        catch (HttpRequestException)
        {
            _error = "Unable to connect to the server. Please check your connection and try again.";
        }
        catch (Exception)
        {
            _error = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            _isChanging = false;
        }
    }

    private async Task HandlePasswordValidationErrorsAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        try
        {
            var errorResponse = JsonSerializer.Deserialize<ValidationErrorResponse>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (errorResponse?.Errors is not null && errorResponse.Errors.Count > 0)
            {
                foreach (var entry in errorResponse.Errors)
                {
                    foreach (var message in entry.Value)
                    {
                        _validationErrors.Add(message);
                    }
                }

                _error = errorResponse.Title ?? "Please fix the following errors:";
            }
            else
            {
                _error = errorResponse?.Title ?? "The request was invalid.";
            }
        }
        catch
        {
            _error = "The request was invalid. Please check your input and try again.";
        }
    }
}