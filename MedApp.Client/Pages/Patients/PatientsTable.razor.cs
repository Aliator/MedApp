using System.Net.Http.Json;
using MedApp.Client.Auth;
using MedApp.Client.Models;
using MedApp.Contracts.Patients.Responses;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Pages.Patients;

public partial class PatientsTable
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "page")] public int? Page { get; set; }
    [SupplyParameterFromQuery(Name = "ps")] public int? PageSize { get; set; }
    [SupplyParameterFromQuery(Name = "sort")] public string? Sort { get; set; }
    [SupplyParameterFromQuery(Name = "asc")] public bool? Asc { get; set; }
    [SupplyParameterFromQuery(Name = "q")] public string? Query { get; set; }
    [SupplyParameterFromQuery(Name = "first")] public string? First { get; set; }
    [SupplyParameterFromQuery(Name = "last")] public string? Last { get; set; }
    [SupplyParameterFromQuery(Name = "dob")] public string? Dob { get; set; }

    private IReadOnlyList<PatientResponse>? _patients;
    private bool _isLoading = true;

    private string _sortColumn = "Patient";
    private bool _sortAscending = true;
    private int _currentPage = 1;
    private int _pageSize = 10;

    private bool _showSearch;
    private PatientSearchCriteria _search = PatientSearchCriteria.Empty;

    private readonly List<SearchFieldDefinition> _searchFields =
    [
        new() { Label = "General Search", Placeholder = "Search across all fields" },
        new() { Label = "First Name", Placeholder = "First name" },
        new() { Label = "Last Name", Placeholder = "Last name" },
        new() { Label = "Date of Birth", Type = SearchFieldType.Date }
    ];

    private bool IsSearchActive => _search.HasFilters;

    private IEnumerable<PatientResponse> FilteredPatients =>
        (_patients ?? Array.Empty<PatientResponse>()).Where(p =>
        {
            var fullName = $"{p.FirstName} {p.LastName}";
            if (_search.HasFilters is false) return true;

            if (!string.IsNullOrWhiteSpace(_search.Query) &&
                !fullName.Contains(_search.Query, StringComparison.OrdinalIgnoreCase) &&
                !p.Email.Contains(_search.Query, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(_search.FirstName) &&
                !p.FirstName.Contains(_search.FirstName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(_search.LastName) &&
                !p.LastName.Contains(_search.LastName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (_search.DateOfBirth is not null && p.DateOfBirth != _search.DateOfBirth)
                return false;

            return true;
        });

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
        _search = PatientSearchCriteria.FromQuery(Query, First, Last, Dob);

        _sortColumn = NormalizeSortColumn(Sort) ?? "Patient";
        _sortAscending = Asc ?? true;
        _pageSize = Math.Max(1, PageSize ?? _pageSize);
        _currentPage = Math.Max(1, Page ?? 1);

        SyncFieldsFromSearch();
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

    private void SyncFieldsFromSearch()
    {
        _searchFields[0].Value = Query ?? string.Empty;
        _searchFields[1].Value = First ?? string.Empty;
        _searchFields[2].Value = Last ?? string.Empty;
        _searchFields[3].DateValue = Dob is not null && DateOnly.TryParse(Dob, out var d) ? d : default;
    }

    private void ApplySearch()
    {
        var dob = _searchFields[3].DateValue != default
            ? _searchFields[3].DateValue.ToString("yyyy-MM-dd")
            : null;

        var criteria = PatientSearchCriteria.FromQuery(
            _searchFields[0].Value,
            _searchFields[1].Value,
            _searchFields[2].Value,
            dob);

        NavigateToState(1, _pageSize, _sortColumn, _sortAscending, criteria, false);
    }

    private void ClearSearch()
    {
        NavigateToState(1, _pageSize, _sortColumn, _sortAscending, PatientSearchCriteria.Empty, false);
    }

    private void HandleSort((string Column, bool Ascending) args)
    {
        NavigateToState(1, _pageSize, args.Column, args.Ascending, _search, false);
    }

    private void HandlePageChange(int page)
    {
        NavigateToState(page, _pageSize, _sortColumn, _sortAscending, _search, false);
    }

    private void HandlePageSizeChanged(int newSize)
    {
        newSize = Math.Max(1, newSize);
        if (newSize == _pageSize) return;

        var targetPage = Math.Min(_currentPage, GetTotalPages(TotalRows, newSize));
        NavigateToState(targetPage, newSize, _sortColumn, _sortAscending, _search, true);
    }

    private void NavigateToState(int page, int pageSize, string sortColumn, bool sortAscending, PatientSearchCriteria criteria, bool replace)
    {
        var sort = NormalizeSortColumn(sortColumn) ?? "Patient";

        var parts = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"ps={Math.Max(1, pageSize)}",
            $"sort={Uri.EscapeDataString(sort)}",
            $"asc={sortAscending.ToString().ToLower()}"
        };

        if (!string.IsNullOrWhiteSpace(criteria.Query))
            parts.Add($"q={Uri.EscapeDataString(criteria.Query)}");
        if (!string.IsNullOrWhiteSpace(criteria.FirstName))
            parts.Add($"first={Uri.EscapeDataString(criteria.FirstName)}");
        if (!string.IsNullOrWhiteSpace(criteria.LastName))
            parts.Add($"last={Uri.EscapeDataString(criteria.LastName)}");
        if (criteria.DateOfBirth is not null)
            parts.Add($"dob={criteria.DateOfBirth.Value.ToString("yyyy-MM-dd")}");

        Nav.NavigateTo("/patients?" + string.Join("&", parts), replace: replace);
    }

    private void ClampCurrentPage()
    {
        _currentPage = Math.Clamp(_currentPage, 1, TotalPages);
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

    private async Task LoadPatientsAsync()
    {
        await Task.Delay(500);
        try
        {
            var response = await Http.GetAsync("api/patients");
            _patients = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<IReadOnlyList<PatientResponse>>()
                : new List<PatientResponse>();
        }
        catch
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
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age;
    }

    private static string GetInitials(string firstName, string lastName)
    {
        var f = !string.IsNullOrEmpty(firstName) ? firstName[0].ToString().ToUpper() : "";
        var l = !string.IsNullOrEmpty(lastName) ? lastName[0].ToString().ToUpper() : "";
        return $"{f}{l}";
    }

    private string GetSubtitle()
    {
        if (_isLoading) return "Loading patients...";
        return $"{_patients?.Count ?? 0} {(_patients?.Count == 1 ? "patient" : "patients")} registered";
    }
}