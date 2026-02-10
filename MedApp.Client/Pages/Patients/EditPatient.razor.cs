using MedApp.Client.Auth;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using Microsoft.AspNetCore.Components;
using System.Net;
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
    private string? _dateError;
    private bool _isSaving;
    private bool _isDeleting;
    private List<string> _validationErrors = [];

    private int _selectedMonth;
    private int _selectedDay;
    private int _selectedYear;

    private int SelectedMonth
    {
        get => _selectedMonth;
        set
        {
            _selectedMonth = value;
            UpdateDateOfBirth();
        }
    }

    private int SelectedDay
    {
        get => _selectedDay;
        set
        {
            _selectedDay = value;
            UpdateDateOfBirth();
        }
    }

    private int SelectedYear
    {
        get => _selectedYear;
        set
        {
            _selectedYear = value;
            UpdateDateOfBirth();
        }
    }

    private async Task LoadPatientAsync()
    {
        try
        {
            var response = await Http.GetAsync($"api/patients/{Id}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Nav.NavigateTo("/patients");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                Nav.NavigateTo("/patients");
                return;
            }

            var patient = await response.Content.ReadFromJsonAsync<PatientResponse>();
            if (patient is null)
            {
                Nav.NavigateTo("/patients");
                return;
            }

            _model = new UpdatePatientRequest
            {
                FirstName = patient.FirstName,
                LastName = patient.LastName,
                DateOfBirth = patient.DateOfBirth,
                Email = patient.Email
            };

            InitializeDateDropdowns(patient.DateOfBirth);
        }
        catch (Exception)
        {
            Nav.NavigateTo("/patients");
        }
    }

    private void InitializeDateDropdowns(DateOnly dateOfBirth)
    {
        _selectedYear = dateOfBirth.Year;
        _selectedMonth = dateOfBirth.Month;
        _selectedDay = dateOfBirth.Day;
    }

    private void UpdateDateOfBirth()
    {
        if (_model is null) return;

        _dateError = null;

        if (_selectedYear > 0 && _selectedMonth > 0 && _selectedDay > 0)
        {
            try
            {
                var date = new DateOnly(_selectedYear, _selectedMonth, _selectedDay);
                
                if (date > DateOnly.FromDateTime(DateTime.Today))
                {
                    _dateError = "Date of birth cannot be in the future";
                    return;
                }

                _model.DateOfBirth = date;
            }
            catch (ArgumentOutOfRangeException)
            {
                _dateError = "Invalid date combination";
            }
        }
    }

    private void ShowSave()
    {
        _errorMessage = null;
        _validationErrors.Clear();
        
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

        try
        {
            var response = await Http.PatchAsJsonAsync($"api/patients/{Id}", _model);

            if (response.IsSuccessStatusCode)
            {
                Nav.NavigateTo($"/patients/{Id}");
                return;
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                await HandleValidationErrorsAsync(response);
                HideSave();
                return;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _errorMessage = "Patient not found. It may have been deleted.";
                HideSave();
                return;
            }

            _errorMessage = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "You are not authorized to edit patients.",
                HttpStatusCode.Forbidden => "You do not have permission to edit this patient.",
                HttpStatusCode.InternalServerError => "A server error occurred. Please try again later.",
                _ => "An unexpected error occurred while saving changes."
            };
        }
        catch (HttpRequestException)
        {
            _errorMessage = "Unable to connect to the server. Please check your connection and try again.";
        }
        catch (Exception)
        {
            _errorMessage = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            _isSaving = false;
        }

        HideSave();
    }

    private async Task HandleValidationErrorsAsync(HttpResponseMessage response)
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

                _errorMessage = errorResponse.Title ?? "Please fix the following errors:";
            }
            else
            {
                _errorMessage = errorResponse?.Title ?? "The request was invalid.";
            }
        }
        catch
        {
            _errorMessage = "The request was invalid. Please check your input and try again.";
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
        _isDeleting = true;

        try
        {
            var response = await Http.DeleteAsync($"api/patients/{Id}");

            if (response.IsSuccessStatusCode)
            {
                Nav.NavigateTo("/patients");
                return;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Nav.NavigateTo("/patients");
                return;
            }

            _errorMessage = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "You are not authorized to delete patients.",
                HttpStatusCode.Forbidden => "You do not have permission to delete this patient.",
                HttpStatusCode.InternalServerError => "A server error occurred. Please try again later.",
                _ => "An unexpected error occurred while deleting the patient."
            };

            HideDelete();
        }
        catch (HttpRequestException)
        {
            _errorMessage = "Unable to connect to the server. Please check your connection and try again.";
            HideDelete();
        }
        catch (Exception)
        {
            _errorMessage = "An unexpected error occurred. Please try again.";
            HideDelete();
        }
        finally
        {
            _isDeleting = false;
        }
    }

    private void Cancel()
    {
        Nav.NavigateTo($"/patients/{Id}");
    }

    private static int CalculateAge(DateOnly dateOfBirth)
    {
        if (dateOfBirth == default)
            return 0;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Year;
        
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }
        
        return age;
    }
}