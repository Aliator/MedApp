using MedApp.Client.Auth;
using MedApp.Contracts.Patients.Requests;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedApp.Contracts.Common;

namespace MedApp.Client.Pages.Patients;

public partial class AddPatient
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private CreatePatientRequest _model = new();
    private bool _confirmSave;
    private bool _isSaving;
    private string? _errorMessage;
    private List<string> _validationErrors = [];

    protected override void OnInitialized()
    {
        if (!Auth.IsAuthenticated)
        {
            Nav.NavigateTo("/login");
            return;
        }

        Auth.Apply(Http);
    }

    private void ShowSave()
    {
        _confirmSave = true;
    }

    private void HideSave()
    {
        _confirmSave = false;
    }

    private async Task Save()
    {
        _errorMessage = null;
        _validationErrors.Clear();
        _isSaving = true;

        var response = await Http.PostAsJsonAsync(
            "api/patients",
            _model);

        _isSaving = false;

        if (response.IsSuccessStatusCode)
        {
            Nav.NavigateTo("/patients");
            return;
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();

            try
            {
                var errorResponse =
                    JsonSerializer.Deserialize<ValidationErrorResponse>(
                        content,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (errorResponse?.Errors is not null && errorResponse.Errors.Count > 0)
                {
                    foreach (var entry in errorResponse.Errors)
                    {
                        foreach (var message in entry.Value)
                        {
                            _validationErrors.Add(message);
                        }
                    }

                    _errorMessage =
                        errorResponse.Title ??
                        "Please fix the following errors:";
                }
                else
                {
                    _errorMessage =
                        errorResponse?.Title ??
                        "The request was invalid.";
                }
            }
            catch
            {
                _errorMessage = "The request was invalid.";
            }

            HideSave();
            return;
        }

        _errorMessage = "An unexpected error occurred while adding the patient.";
        HideSave();
    }

    private void Cancel()
    {
        Nav.NavigateTo("/patients");
    }
}
