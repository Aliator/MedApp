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
    private bool _isLoading = true;

    private string _sortColumn = "Patient";
    private bool _sortAscending = true;
    private int _currentPage = 1;
    private int _pageSize = 10;

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)(_patients?.Count ?? 0) / _pageSize));

    private IEnumerable<PatientResponse> SortedPatients => _sortColumn switch
    {
        "Patient" => _sortAscending
            ? (_patients ?? Array.Empty<PatientResponse>()).OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            : (_patients ?? Array.Empty<PatientResponse>()).OrderByDescending(p => p.LastName).ThenByDescending(p => p.FirstName),
        "DateOfBirth" => _sortAscending
            ? (_patients ?? Array.Empty<PatientResponse>()).OrderBy(p => p.DateOfBirth)
            : (_patients ?? Array.Empty<PatientResponse>()).OrderByDescending(p => p.DateOfBirth),
        "Email" => _sortAscending
            ? (_patients ?? Array.Empty<PatientResponse>()).OrderBy(p => p.Email, StringComparer.OrdinalIgnoreCase)
            : (_patients ?? Array.Empty<PatientResponse>()).OrderByDescending(p => p.Email, StringComparer.OrdinalIgnoreCase),
        _ => _patients ?? Array.Empty<PatientResponse>()
    };

    private IEnumerable<PatientResponse> PagedPatients =>
        SortedPatients.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);

    private void HandleSort((string Column, bool Ascending) args)
    {
        _sortColumn = args.Column;
        _sortAscending = args.Ascending;
        _currentPage = 1;
    }

    private void HandlePageChange(int page) => _currentPage = page;

    private void HandlePageSizeChanged(int newSize)
    {
        _pageSize = newSize;
        var totalRows = _patients?.Count ?? 0;
        var maxPage = Math.Max(1, (int)Math.Ceiling((double)totalRows / _pageSize));
        if (_currentPage > maxPage) _currentPage = maxPage;
        StateHasChanged();
    }

    protected override async Task OnInitializedAsync()
    {
        if (!await Auth.EnsureAuthenticatedAsync())
        {
            Nav.NavigateTo("/login", true);
            return;
        }

        await LoadPatientsAsync();
        _isLoading = false;
    }

    private async Task LoadPatientsAsync()
    {
        await Task.Delay(500);
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

    private void ViewPatient(Guid id) => Nav.NavigateTo($"/patients/{id}");

    private void EditPatient(Guid id) => Nav.NavigateTo($"/patients/{id}/edit");

    private void AddPatient() => Nav.NavigateTo("/patients/add");

    private static int CalculateAge(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Year;

        if (dateOfBirth > today.AddYears(-age))
            age--;

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
        if (_isLoading) return "Loading patients...";
        return $"{_patients?.Count ?? 0} {(_patients?.Count == 1 ? "patient" : "patients")} registered";
    }
}