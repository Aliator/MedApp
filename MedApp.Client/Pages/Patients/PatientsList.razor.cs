using System.Net.Http.Json;
using MedApp.Client.Auth;
using MedApp.Contracts.Patients.Responses;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Pages.Patients;

public partial class PatientsList
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private IReadOnlyList<PatientResponse>? _patients;

    protected override async Task OnInitializedAsync()
    {
        if (!await Auth.EnsureAuthenticatedAsync())
        {
            Nav.NavigateTo("/login", true);
            return;
        }

        await LoadPatientsAsync();
    }

    private async Task LoadPatientsAsync()
    {
        try
        {
            var response = await Http.GetAsync("api/patients");
            
            if (response.IsSuccessStatusCode)
            {
                _patients = await response.Content.ReadFromJsonAsync<IReadOnlyList<PatientResponse>>();
            }
            else
            {
                _patients = new List<PatientResponse>();
            }
        }
        catch (Exception)
        {
            _patients = new List<PatientResponse>();
        }
    }

    private void ViewPatient(Guid id)
    {
        Nav.NavigateTo($"/patients/{id}");
    }

    private void EditPatient(Guid id)
    {
        Nav.NavigateTo($"/patients/{id}/edit");
    }
    
    private void AddPatient()
    {
        Nav.NavigateTo("/patients/add");
    }

    private static int CalculateAge(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Year;
        
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }
        
        return age;
    }

    private static string GetInitials(string firstName, string lastName)
    {
        var firstInitial = !string.IsNullOrEmpty(firstName) ? firstName[0].ToString().ToUpper() : "";
        var lastInitial = !string.IsNullOrEmpty(lastName) ? lastName[0].ToString().ToUpper() : "";
        return $"{firstInitial}{lastInitial}";
    }
    
    private string GetSubtitle()
    {
        if (_patients is null)
            return "Loading patients...";

        return $"{_patients.Count} {(_patients.Count == 1 ? "patient" : "patients")} registered";
    }
}