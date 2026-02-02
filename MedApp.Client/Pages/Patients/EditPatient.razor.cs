using MedApp.Client.Auth;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using System.Text.Json;
using MedApp.Contracts.Common;


namespace MedApp.Client.Pages.Patients;

public partial class EditPatient
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private UpdatePatientRequest? _model;
    private bool _confirmDelete;
    private bool _confirmSave;
    private string? _errorMessage;
    private bool _isSaving;
    private List<string> _validationErrors = [];
    
    protected override async Task OnInitializedAsync()
    {
        if (!Auth.IsAuthenticated)
        {
            Nav.NavigateTo("/login");
            return;
        }

        Auth.Apply(Http);

        var response = await Http.GetAsync($"api/patients/{Id}");
        if (!response.IsSuccessStatusCode)
            return;

        var patient = await response.Content.ReadFromJsonAsync<PatientResponse>();
        if (patient is null)
            return;

        _model = new UpdatePatientRequest
        {
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Email = patient.Email
        };
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
        if (_model is null)
            return;

        _errorMessage = null;
        _validationErrors.Clear();
        _isSaving = true;

        var response = await Http.PatchAsJsonAsync(
            $"api/patients/{Id}",
            _model);

        _isSaving = false;

        if (response.IsSuccessStatusCode)
        {
            Nav.NavigateTo("/patients");
            return;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();

            _validationErrors.Clear();
            _errorMessage = "There were one or more unknown errors.";

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
                    _errorMessage = "Please fix the following errors:";
                    
                    foreach (var entry in errorResponse.Errors)
                    {
                        foreach (var message in entry.Value)
                        {
                            _validationErrors.Add(message);
                        }
                    }
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
        }
    }
    
    private void ShowDelete()
    {
        _confirmDelete = true;
    }

    private void HideDelete()
    {
        _confirmDelete = false;
    }

    private async Task Delete()
    {
        var response = await Http.DeleteAsync($"api/patients/{Id}");
        if (!response.IsSuccessStatusCode)
            return;

        Nav.NavigateTo("/patients");
    }
}
