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

    private IReadOnlyList<string>? _users;
    private IReadOnlyList<string> _roles = Array.Empty<string>();
    private Dictionary<string, IReadOnlyList<string>> _userRolesCache = new(StringComparer.OrdinalIgnoreCase);

    private bool _isLoading = true;
    private string _loadingMessage = "Loading users...";

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
        }
        finally
        {
            _isLoading = false;
        }
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
            if (response.IsSuccessStatusCode)
            {
                _roles = await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>() ?? Array.Empty<string>();
            }
            else
            {
                _roles = Array.Empty<string>();
            }
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
        {
            _userRolesCache[user] = await LoadRolesForUserAsync(user);
        }
    }

    private async Task<IReadOnlyList<string>> LoadRolesForUserAsync(string username)
    {
        try
        {
            var response = await Http.GetAsync($"api/auth/users/{username}/roles");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>() ?? Array.Empty<string>();
            }
        }
        catch
        {
            // ignored
        }

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
        if (string.IsNullOrEmpty(_selectedUser) || !_selectedRoles.Any())
            return;

        _isProcessingRoles = true;
        try
        {
            foreach (var role in _selectedRoles)
            {
                await Http.PostAsJsonAsync("api/auth/roles/assign", new AssignRoleRequest(_selectedUser, role));
            }

            _userRolesCache[_selectedUser] = await LoadRolesForUserAsync(_selectedUser);
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
        if (string.IsNullOrEmpty(_selectedUser) || !_selectedRoles.Any())
            return;

        _isProcessingRoles = true;
        try
        {
            foreach (var role in _selectedRoles)
            {
                await Http.PostAsJsonAsync("api/auth/roles/revoke", new RevokeRoleRequest(_selectedUser, role));
            }

            _userRolesCache[_selectedUser] = await LoadRolesForUserAsync(_selectedUser);
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
        if (string.IsNullOrEmpty(_selectedUser))
            return;

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
        if (string.IsNullOrEmpty(_selectedUser))
            return;

        _isDeletingUser = true;
        try
        {
            var response = await Http.DeleteAsync($"api/auth/users/{_selectedUser}");
            if (response.IsSuccessStatusCode)
            {
                _userRolesCache.Remove(_selectedUser);
                await LoadUsersAsync();
                await LoadAllUserRolesAsync();
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
        if (string.IsNullOrWhiteSpace(_newUsername))
            return;

        _isCreatingUser = true;
        try
        {
            var response = await Http.PostAsJsonAsync("api/auth/users", new CreateUserRequest(_newUsername, "DefaultPassword1"));
            if (response.IsSuccessStatusCode)
            {
                HideAddUser();
                await LoadUsersAsync();
                await LoadAllUserRolesAsync();
            }
        }
        finally
        {
            _isCreatingUser = false;
        }
    }

    private void OnRoleToggled(SelectionToggle<string> e)
    {
        if (e.IsSelected)
            _selectedRoles.Add(e.Item);
        else
            _selectedRoles.Remove(e.Item);
    }

    private string GetInitial(string username) =>
        !string.IsNullOrEmpty(username) ? username[0].ToString().ToUpper() : "?";
}
