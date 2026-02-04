using System.Net.Http.Json;
using MedApp.Client.Auth;
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
    
    // Cache for user roles to display in table
    private Dictionary<string, IReadOnlyList<string>> _userRolesCache = new();

    private bool _showAssignRole;
    private bool _showRevokeRole;
    private bool _showResetPassword;
    private bool _confirmDelete;
    private bool _showAddUser;
    
    private string _newUsername = string.Empty;
    private string _newPassword = string.Empty;
    
    private IReadOnlyList<string> _userRoles = Array.Empty<string>();
    
    private IEnumerable<string> AssignableRoles =>
        _roles.Except(_userRoles, StringComparer.OrdinalIgnoreCase);

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

        await LoadUsersAsync();
        await LoadRolesAsync();
        await LoadAllUserRolesAsync();
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var response = await Http.GetAsync("api/auth/users");
            
            if (response.IsSuccessStatusCode)
            {
                _users = await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>();
            }
            else
            {
                _users = new List<string>();
            }
        }
        catch (Exception)
        {
            _users = new List<string>();
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
        }
        catch (Exception)
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
            var userRoles = await LoadRolesForUserAsync(user);
            _userRolesCache[user] = userRoles;
        }

        StateHasChanged();
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
        catch (Exception)
        {
            // Return empty if error
        }

        return Array.Empty<string>();
    }

    // Assign Roles
    private async Task ShowAssignRole(string username)
    {
        _selectedUser = username;
        _selectedRoles.Clear();

        // Load current roles for the selected user
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

        try
        {
            foreach (var role in _selectedRoles)
            {
                await Http.PostAsJsonAsync(
                    "api/auth/roles/assign",
                    new AssignRoleRequest(_selectedUser, role));
            }

            // Refresh user roles cache
            if (_selectedUser != null)
            {
                var updatedRoles = await LoadRolesForUserAsync(_selectedUser);
                _userRolesCache[_selectedUser] = updatedRoles;
            }
        }
        catch (Exception)
        {
            // Handle error
        }

        HideAssignRole();
    }

    private async Task ShowRevokeRole(string username)
    {
        _selectedUser = username;
        _selectedRoles.Clear();

        // Load current roles for the selected user
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

        try
        {
            // DELETE api/auth/users/{username}/roles/{role}
            foreach (var role in _selectedRoles)
            {
                // await Http.DeleteAsync($"api/auth/users/{_selectedUser}/roles/{role}");
            }

            // Refresh user roles cache
            if (_selectedUser != null)
            {
                var updatedRoles = await LoadRolesForUserAsync(_selectedUser);
                _userRolesCache[_selectedUser] = updatedRoles;
            }
        }
        catch (Exception)
        {
            // Handle error
        }

        HideRevokeRole();
    }

    // Reset Password
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

        try
        {
            // Call your existing endpoint: PUT api/auth/users/{username}/reset-password
            var response = await Http.PutAsync($"api/auth/users/{_selectedUser}/reset-password", null);

            if (response.IsSuccessStatusCode)
            {
                // Your endpoint returns the password directly as a string
                _newPassword = await response.Content.ReadAsStringAsync();
                
                // Remove quotes if present
                _newPassword = _newPassword.Trim('"');
                
                StateHasChanged();
            }
        }
        catch (Exception)
        {
            // Handle error
        }
    }

    private void ToggleRole(string role, bool isChecked)
    {
        if (isChecked)
            _selectedRoles.Add(role);
        else
            _selectedRoles.Remove(role);
    }

    // Delete User
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

        try
        {
            var response = await Http.DeleteAsync($"api/auth/users/{_selectedUser}");

            if (response.IsSuccessStatusCode)
            {
                // Remove from cache
                _userRolesCache.Remove(_selectedUser);
                
                HideDelete();
                await LoadUsersAsync();
            }
        }
        catch (Exception)
        {
            // Handle error
            HideDelete();
        }
    }

    // Add User
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

        try
        {
            var response = await Http.PostAsJsonAsync(
                "api/auth/users",
                new CreateUserRequest(_newUsername, "DefaultPassword1"));

            if (response.IsSuccessStatusCode)
            {
                HideAddUser();
                await LoadUsersAsync();
                await LoadAllUserRolesAsync();
            }
        }
        catch (Exception)
        {
            // Handle error
        }
    }

    private string GetInitial(string username)
    {
        return !string.IsNullOrEmpty(username) 
            ? username[0].ToString().ToUpper() 
            : "?";
    }
}