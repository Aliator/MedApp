using System.Net.Http.Json;
using MedApp.Client.Auth;
using MedApp.Client.Components.Modals;
using MedApp.Contracts.Patients.Responses;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Pages.Patients;

public partial class PatientsList
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "page")] public int? Page { get; set; }
    [SupplyParameterFromQuery(Name = "ps")] public int? PageSize { get; set; }
    [SupplyParameterFromQuery(Name = "sort")] public string? Sort { get; set; }
    [SupplyParameterFromQuery(Name = "asc")] public bool? Asc { get; set; }
    [SupplyParameterFromQuery(Name = "name")] public string? Name { get; set; }
    [SupplyParameterFromQuery(Name = "email")] public string? Email { get; set; }
    [SupplyParameterFromQuery(Name = "agemin")] public int? AgeMin { get; set; }
    [SupplyParameterFromQuery(Name = "agemax")] public int? AgeMax { get; set; }

    private IReadOnlyList<PatientResponse>? _patients;
    private bool _isLoading = true;

    private string _sortColumn = "Patient";
    private bool _sortAscending = true;
    private int _currentPage = 1;
    private int _pageSize = 10;

    private bool _showSearch;
    private PatientSearchCriteria _search = new(string.Empty, string.Empty, null, null);

    private bool IsSearchActive =>
        !string.IsNullOrWhiteSpace(_search.Name) ||
        !string.IsNullOrWhiteSpace(_search.Email) ||
        _search.AgeMin is not null ||
        _search.AgeMax is not null;

    private IEnumerable<PatientResponse> FilteredPatients =>
        (_patients ?? Array.Empty<PatientResponse>()).Where(p =>
            (string.IsNullOrWhiteSpace(_search.Name) ||
             $"{p.FirstName} {p.LastName}".Contains(_search.Name, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(_search.Email) ||
             p.Email.Contains(_search.Email, StringComparison.OrdinalIgnoreCase)) &&
            (_search.AgeMin == null || CalculateAge(p.DateOfBirth) >= _search.AgeMin) &&
            (_search.AgeMax == null || CalculateAge(p.DateOfBirth) <= _search.AgeMax));

    private int TotalRows => FilteredPatients.Count();

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalRows / _pageSize));

    private IEnumerable<PatientResponse> SortedPatients => _sortColumn switch
    {
        "Patient" => _sortAscending
            ? FilteredPatients.OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            : FilteredPatients.OrderByDescending(p => p.LastName).ThenByDescending(p => p.FirstName),
        "DateOfBirth" => _sortAscending
            ? FilteredPatients.OrderBy(p => p.DateOfBirth)
            : FilteredPatients.OrderByDescending(p => p.DateOfBirth),
        "Email" => _sortAscending
            ? FilteredPatients.OrderBy(p => p.Email, StringComparer.OrdinalIgnoreCase)
            : FilteredPatients.OrderByDescending(p => p.Email, StringComparer.OrdinalIgnoreCase),
        _ => FilteredPatients
    };

    private IEnumerable<PatientResponse> PagedPatients =>
        SortedPatients.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);

    protected override void OnParametersSet()
    {
        _search = new PatientSearchCriteria(Name ?? string.Empty, Email ?? string.Empty, AgeMin, AgeMax);

        _sortColumn = NormalizeSortColumn(Sort) ?? "Patient";
        _sortAscending = Asc ?? true;

        _pageSize = Math.Max(1, PageSize ?? _pageSize);
        _currentPage = Math.Max(1, Page ?? 1);
        ClampCurrentPage();
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
        ClampCurrentPage();
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
        catch
        {
            _patients = new List<PatientResponse>();
        }
    }

    private void HandleSearch(PatientSearchCriteria criteria)
    {
        NavigateToState(page: 1, pageSize: _pageSize, sortColumn: _sortColumn, sortAscending: _sortAscending, criteria: criteria, replace: false);
    }

    private void ClearSearch()
    {
        var empty = new PatientSearchCriteria(string.Empty, string.Empty, null, null);
        NavigateToState(page: 1, pageSize: _pageSize, sortColumn: _sortColumn, sortAscending: _sortAscending, criteria: empty, replace: false);
    }

    private void HandleSort((string Column, bool Ascending) args)
    {
        NavigateToState(page: 1, pageSize: _pageSize, sortColumn: args.Column, sortAscending: args.Ascending, criteria: _search, replace: false);
    }

    private void HandlePageChange(int page)
    {
        NavigateToState(page: page, pageSize: _pageSize, sortColumn: _sortColumn, sortAscending: _sortAscending, criteria: _search, replace: false);
    }

    private void HandlePageSizeChanged(int newSize)
    {
        newSize = Math.Max(1, newSize);
        if (newSize == _pageSize) return;

        var maxPage = GetTotalPages(TotalRows, newSize);
        var targetPage = Math.Min(_currentPage, maxPage);

        NavigateToState(page: targetPage, pageSize: newSize, sortColumn: _sortColumn, sortAscending: _sortAscending, criteria: _search, replace: true);
    }

    private void NavigateToState(int page, int pageSize, string sortColumn, bool sortAscending, PatientSearchCriteria criteria, bool replace)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var sort = NormalizeSortColumn(sortColumn) ?? "Patient";

        var parts = new List<string>
        {
            $"page={page}",
            $"ps={pageSize}",
            $"sort={Uri.EscapeDataString(sort)}",
            $"asc={(sortAscending ? "true" : "false")}"
        };

        if (!string.IsNullOrWhiteSpace(criteria.Name))
            parts.Add($"name={Uri.EscapeDataString(criteria.Name)}");

        if (!string.IsNullOrWhiteSpace(criteria.Email))
            parts.Add($"email={Uri.EscapeDataString(criteria.Email)}");

        if (criteria.AgeMin is not null)
            parts.Add($"agemin={criteria.AgeMin.Value}");

        if (criteria.AgeMax is not null)
            parts.Add($"agemax={criteria.AgeMax.Value}");

        var uri = "/patients" + "?" + string.Join("&", parts);
        Nav.NavigateTo(uri, replace: replace);
    }

    private void ClampCurrentPage()
    {
        var maxPage = TotalPages;
        if (_currentPage > maxPage) _currentPage = maxPage;
        if (_currentPage < 1) _currentPage = 1;
    }

    private static int GetTotalPages(int totalRows, int pageSize) =>
        Math.Max(1, (int)Math.Ceiling((double)totalRows / Math.Max(1, pageSize)));

    private static string? NormalizeSortColumn(string? value)
    {
        if (string.Equals(value, "Patient", StringComparison.OrdinalIgnoreCase)) return "Patient";
        if (string.Equals(value, "DateOfBirth", StringComparison.OrdinalIgnoreCase)) return "DateOfBirth";
        if (string.Equals(value, "Email", StringComparison.OrdinalIgnoreCase)) return "Email";
        return null;
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
