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
        if (!Auth.IsAuthenticated)
        {
            Nav.NavigateTo("/login", true);
            return;
        }

        Auth.Apply(Http);

        var response = await Http.GetAsync("api/patients");
        if (!response.IsSuccessStatusCode)
            return;

        _patients = await response.Content.ReadFromJsonAsync<IReadOnlyList<PatientResponse>>();
    }

    private void ViewPatient(Guid id)
    {
        Nav.NavigateTo($"/patients/{id}");
    }
    private void AddPatient()
    {
        Nav.NavigateTo("/patients/add");
    }

}