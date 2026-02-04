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
        if (!Auth.IsAuthenticated)
        {
            Nav.NavigateTo("/login", true);
            return;
        }

        var response = await Http.GetAsync($"api/patients/{Id}");
        if (!response.IsSuccessStatusCode)
            return;

        _patient = await response.Content.ReadFromJsonAsync<PatientResponse>();
    }

    private void GoBack()
    {
        Nav.NavigateTo("/patients");
    }
    
    private void Edit()
    {
        Nav.NavigateTo($"/patients/{Id}/edit");
    }

}