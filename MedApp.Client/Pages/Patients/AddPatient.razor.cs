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

    private readonly CreatePatientRequest _model = new();
    private bool _confirmSave;
    private bool _isSaving;
    private string? _errorMessage;
    private string? _dateError;
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

    private void UpdateDateOfBirth()
    {
        _dateError = null;

        if (_selectedYear > 0 && _selectedMonth > 0 && _selectedDay > 0)
        {
            try
            {
                var date = new DateOnly(_selectedYear, _selectedMonth, _selectedDay);
                
                if (date > DateOnly.FromDateTime(DateTime.Today))
                {
                    _dateError = "Date of birth cannot be in the future";
                    _model.DateOfBirth = default;
                    return;
                }

                _model.DateOfBirth = date;
            }
            catch (ArgumentOutOfRangeException)
            {
                _dateError = "Invalid date combination";
                _model.DateOfBirth = default;
            }
        }
        else
        {
            _model.DateOfBirth = default;
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
        _errorMessage = null;
        _validationErrors.Clear();
        _isSaving = true;

        try
        {
            var response = await Http.PostAsJsonAsync("api/patients", _model);

            if (response.IsSuccessStatusCode)
            {
                Nav.NavigateTo("/patients");
                return;
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                await HandleValidationErrorsAsync(response);
                HideSave();
                return;
            }

            _errorMessage = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "You are not authorized to add patients.",
                HttpStatusCode.Forbidden => "You do not have permission to add patients.",
                HttpStatusCode.InternalServerError => "A server error occurred. Please try again later.",
                _ => "An unexpected error occurred while adding the patient."
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

    private void Cancel()
    {
        Nav.NavigateTo("/patients");
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