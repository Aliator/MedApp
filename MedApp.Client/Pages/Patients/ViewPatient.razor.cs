using System.Net.Http.Json;
using MedApp.Client.Auth;
using MedApp.Contracts.Patients.Responses;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Pages.Patients;

public partial class ViewPatient
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private PatientResponse? _patient;

    protected override async Task OnInitializedAsync()
    {
        if (!await Auth.EnsureAuthenticatedAsync())
        {
            Nav.NavigateTo("/login", true);
            return;
        }

        await LoadPatientAsync();
    }

    private async Task LoadPatientAsync()
    {
        try
        {
            var response = await Http.GetAsync($"api/patients/{Id}");
            
            if (response.IsSuccessStatusCode)
            {
                _patient = await response.Content.ReadFromJsonAsync<PatientResponse>();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Patient not found, redirect to list
                Nav.NavigateTo("/patients");
            }
        }
        catch (Exception)
        {
            // Handle error gracefully - could add error state here
            Nav.NavigateTo("/patients");
        }
    }

    private void GoBack()
    {
        Nav.NavigateTo("/patients");
    }
    
    private void Edit()
    {
        Nav.NavigateTo($"/patients/{Id}/edit");
    }

    private int CalculateAge(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Year;
        
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }
        
        return age;
    }
}