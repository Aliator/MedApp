using System.Net.Http.Json;
using MedApp.Client.Auth;
using MedApp.Client.Components.Modals;
using MedApp.Contracts.Auth.Requests;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Pages.Admin.Users;

public partial class UsersList
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private AuthState Auth { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "page")] public int? Page { get; set; }
    [SupplyParameterFromQuery(Name = "ps")] public int? PageSize { get; set; }
    [SupplyParameterFromQuery(Name = "sort")] public string? Sort { get; set; }
    [SupplyParameterFromQuery(Name = "asc")] public bool? Asc { get; set; }
    [SupplyParameterFromQuery(Name = "username")] public string? Username { get; set; }
    [SupplyParameterFromQuery(Name = "role")] public string? Role { get; set; }

    private IReadOnlyList<string>? _users;
    private IReadOnlyList<string> _roles = Array.Empty<string>();
    private Dictionary<string, IReadOnlyList<string>> _userRolesCache = new(StringComparer.OrdinalIgnoreCase);

    private bool _isLoading = true;
    private string _loadingMessage = "Loading users...";

    private bool _showSearch;
    private UserSearchCriteria _search = new(string.Empty, string.Empty);

    private bool _showAssignRole;
    private bool _showRevokeRole;
    private bool _showResetPassword;
    private bool _confirmDelete;
    private bool _showAddUser;

    private bool _isProcessingRoles;
    private bool _isResettingPassword;
    private bool _isDeletingUser;
    private bool _isCreatingUser;

    private string _newUsername = string.Empty;
    private string _newPassword = string.Empty;

    private IReadOnlyList<string> _userRoles = Array.Empty<string>();

    private IEnumerable<string> AssignableRoles =>
        _roles.Except(_userRoles, StringComparer.OrdinalIgnoreCase);

    private IEnumerable<string> RevokableRoles =>
        _userRoles.Distinct(StringComparer.OrdinalIgnoreCase);

    private string? _selectedUser;
    private HashSet<string> _selectedRoles = new(StringComparer.OrdinalIgnoreCase);

    private string _sortColumn = "User";
    private bool _sortAscending = true;
    private int _currentPage = 1;
    private int _pageSize = 10;

    private bool IsSearchActive =>
        !string.IsNullOrWhiteSpace(_search.Username) ||
        !string.IsNullOrWhiteSpace(_search.Role);

    private IEnumerable<string> FilteredUsers =>
        (_users ?? Array.Empty<string>()).Where(u =>
            (string.IsNullOrWhiteSpace(_search.Username) ||
             u.Contains(_search.Username, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(_search.Role) ||
             (_userRolesCache.TryGetValue(u, out var roles) &&
              roles.Any(r => r.Contains(_search.Role, StringComparison.OrdinalIgnoreCase)))));

    private int TotalRows => FilteredUsers.Count();

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalRows / _pageSize));

    private IEnumerable<string> SortedUsers => _sortColumn switch
    {
        "User" => _sortAscending
            ? FilteredUsers.OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            : FilteredUsers.OrderByDescending(u => u, StringComparer.OrdinalIgnoreCase),
        _ => FilteredUsers
    };

    private IEnumerable<string> PagedUsers =>
        SortedUsers.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);

    protected override void OnParametersSet()
    {
        _search = new UserSearchCriteria(Username ?? string.Empty, Role ?? string.Empty);

        _sortColumn = NormalizeSortColumn(Sort) ?? "User";
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

        if (!Auth.IsAdmin)
        {
            Nav.NavigateTo("/", true);
            return;
        }

        try
        {
            _isLoading = true;
            _loadingMessage = "Loading users...";

            await LoadUsersAsync();
            await LoadRolesAsync();
            await LoadAllUserRolesAsync();
            ClampCurrentPage();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void HandleSearch(UserSearchCriteria criteria)
    {
        NavigateToState(page: 1, pageSize: _pageSize, sortColumn: _sortColumn, sortAscending: _sortAscending, criteria: criteria, replace: false);
    }

    private void ClearSearch()
    {
        var empty = new UserSearchCriteria(string.Empty, string.Empty);
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

    private void NavigateToState(int page, int pageSize, string sortColumn, bool sortAscending, UserSearchCriteria criteria, bool replace)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var sort = NormalizeSortColumn(sortColumn) ?? "User";

        var parts = new List<string>
        {
            $"page={page}",
            $"ps={pageSize}",
            $"sort={Uri.EscapeDataString(sort)}",
            $"asc={(sortAscending ? "true" : "false")}"
        };

        if (!string.IsNullOrWhiteSpace(criteria.Username))
            parts.Add($"username={Uri.EscapeDataString(criteria.Username)}");

        if (!string.IsNullOrWhiteSpace(criteria.Role))
            parts.Add($"role={Uri.EscapeDataString(criteria.Role)}");

        var uri = "/admin/users" + "?" + string.Join("&", parts);
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
        if (string.Equals(value, "User", StringComparison.OrdinalIgnoreCase)) return "User";
        return null;
    }

    private string GetSubtitle()
    {
        var count = _users?.Count ?? 0;
        return $"{count} {(count == 1 ? "user" : "users")} in the system";
    }

    private string GetSelectionMessage(string mode)
    {
        var name = _selectedUser ?? "this user";
        return mode == "assign"
            ? $"Select roles to assign to {name}."
            : $"Select roles to revoke from {name}.";
    }

    private string GetResetPasswordMessage()
    {
        if (!string.IsNullOrEmpty(_newPassword))
            return $"Password reset for {_selectedUser}.";
        return $"Reset password for {_selectedUser}?";
    }

    private async Task LoadUsersAsync()
    {
        await Task.Delay(500);
        try
        {
            var response = await Http.GetAsync("api/auth/users");
            _users = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>() ?? Array.Empty<string>()
                : Array.Empty<string>();
        }
        catch
        {
            _users = Array.Empty<string>();
        }
    }

    private async Task LoadRolesAsync()
    {
        try
        {
            var response = await Http.GetAsync("api/auth/roles");
            _roles = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>() ?? Array.Empty<string>()
                : Array.Empty<string>();
        }
        catch
        {
            _roles = Array.Empty<string>();
        }
    }

    private async Task LoadAllUserRolesAsync()
    {
        if (_users is null) return;

        _userRolesCache.Clear();
        foreach (var user in _users)
            _userRolesCache[user] = await LoadRolesForUserAsync(user);
    }

    private async Task<IReadOnlyList<string>> LoadRolesForUserAsync(string username)
    {
        try
        {
            var response = await Http.GetAsync($"api/auth/users/{username}/roles");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>() ?? Array.Empty<string>();
        }
        catch { }

        return Array.Empty<string>();
    }

    private async Task ShowAssignRole(string username)
    {
        _selectedUser = username;
        _selectedRoles.Clear();
        _userRoles = await LoadRolesForUserAsync(username);
        _showAssignRole = true;
    }

    private void HideAssignRole()
    {
        _showAssignRole = false;
        _selectedUser = null;
        _selectedRoles.Clear();
        _userRoles = Array.Empty<string>();
    }

    private async Task AssignRoles()
    {
        if (string.IsNullOrEmpty(_selectedUser) || !_selectedRoles.Any()) return;

        _isProcessingRoles = true;
        try
        {
            foreach (var role in _selectedRoles)
                await Http.PostAsJsonAsync("api/auth/roles/assign", new AssignRoleRequest(_selectedUser, role));

            _userRolesCache[_selectedUser] = await LoadRolesForUserAsync(_selectedUser);
            ClampCurrentPage();
        }
        finally
        {
            _isProcessingRoles = false;
            HideAssignRole();
        }
    }

    private async Task ShowRevokeRole(string username)
    {
        _selectedUser = username;
        _selectedRoles.Clear();
        _userRoles = await LoadRolesForUserAsync(username);
        _showRevokeRole = true;
    }

    private void HideRevokeRole()
    {
        _showRevokeRole = false;
        _selectedUser = null;
        _selectedRoles.Clear();
        _userRoles = Array.Empty<string>();
    }

    private async Task RevokeRoles()
    {
        if (string.IsNullOrEmpty(_selectedUser) || !_selectedRoles.Any()) return;

        _isProcessingRoles = true;
        try
        {
            foreach (var role in _selectedRoles)
                await Http.PostAsJsonAsync("api/auth/roles/revoke", new RevokeRoleRequest(_selectedUser, role));

            _userRolesCache[_selectedUser] = await LoadRolesForUserAsync(_selectedUser);
            ClampCurrentPage();
        }
        finally
        {
            _isProcessingRoles = false;
            HideRevokeRole();
        }
    }

    private void ShowResetPassword(string username)
    {
        _selectedUser = username;
        _newPassword = string.Empty;
        _showResetPassword = true;
    }

    private void HideResetPassword()
    {
        _showResetPassword = false;
        _selectedUser = null;
        _newPassword = string.Empty;
    }

    private async Task ResetPassword()
    {
        if (string.IsNullOrEmpty(_selectedUser)) return;

        _isResettingPassword = true;
        try
        {
            var response = await Http.PutAsync($"api/auth/users/{_selectedUser}/reset-password", null);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _newPassword = body.Trim('"');
            }
        }
        finally
        {
            _isResettingPassword = false;
        }
    }

    private void ShowDelete(string username)
    {
        _selectedUser = username;
        _confirmDelete = true;
    }

    private void HideDelete()
    {
        _confirmDelete = false;
        _selectedUser = null;
    }

    private async Task DeleteUser()
    {
        if (string.IsNullOrEmpty(_selectedUser)) return;

        _isDeletingUser = true;
        try
        {
            var response = await Http.DeleteAsync($"api/auth/users/{_selectedUser}");
            if (response.IsSuccessStatusCode)
            {
                _userRolesCache.Remove(_selectedUser);
                await LoadUsersAsync();
                await LoadAllUserRolesAsync();
                ClampCurrentPage();
            }
        }
        finally
        {
            _isDeletingUser = false;
            HideDelete();
        }
    }

    private void ShowAddUser()
    {
        _newUsername = string.Empty;
        _showAddUser = true;
    }

    private void HideAddUser()
    {
        _showAddUser = false;
        _newUsername = string.Empty;
    }

    private async Task CreateUser()
    {
        if (string.IsNullOrWhiteSpace(_newUsername)) return;

        _isCreatingUser = true;
        try
        {
            var response = await Http.PostAsJsonAsync("api/auth/users", new CreateUserRequest(_newUsername, "DefaultPassword1"));
            if (response.IsSuccessStatusCode)
            {
                HideAddUser();
                await LoadUsersAsync();
                await LoadAllUserRolesAsync();
                ClampCurrentPage();
            }
        }
        finally
        {
            _isCreatingUser = false;
        }
    }

    private void OnRoleToggled(SelectionToggle<string> e)
    {
        if (e.IsSelected) _selectedRoles.Add(e.Item);
        else _selectedRoles.Remove(e.Item);
    }

    private string GetInitial(string username) =>
        !string.IsNullOrEmpty(username) ? username[0].ToString().ToUpper() : "?";
}
