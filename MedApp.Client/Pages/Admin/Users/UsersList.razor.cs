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

    private bool _showAssignRole;
    private bool _confirmDelete;
    
    private bool _showAddUser;
    private string _newUsername = string.Empty;

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

        await LoadUsers();
        await LoadRoles();
    }

    private async Task LoadUsers()
    {
        var response = await Http.GetAsync("api/auth/users");
        if (!response.IsSuccessStatusCode)
            return;

        _users = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<string>>();
    }

    private async Task LoadRoles()
    {
        var response = await Http.GetAsync("api/auth/roles");
        if (!response.IsSuccessStatusCode)
            return;

        _roles = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<string>>() ?? Array.Empty<string>();
    }
    
    private async Task ShowAssignRole(string username)
    {
        _selectedUser = username;
        _selectedRoles.Clear();

        await LoadRolesForUser(username);

        _showAssignRole = true;
    }

    private void HideAssignRole()
    {
        _showAssignRole = false;
        _selectedUser = null;
        _selectedRoles.Clear();
    }

    private async Task AssignRoles()
    {
        if (string.IsNullOrEmpty(_selectedUser) || !_selectedRoles.Any())
            return;

        foreach (var role in _selectedRoles)
        {
            await Http.PostAsJsonAsync(
                "api/auth/roles/assign",
                new AssignRoleRequest(_selectedUser, role));
        }

        HideAssignRole();
    }
    
    private void ToggleRole(string role, bool isChecked)
    {
        if (isChecked)
            _selectedRoles.Add(role);
        else
            _selectedRoles.Remove(role);
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

        await Http.DeleteAsync($"api/auth/users/{_selectedUser}");

        HideDelete();
        await LoadUsers();
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

        var response = await Http.PostAsJsonAsync(
            "api/auth/users",
            new CreateUserRequest(
                _newUsername,
                "DefaultPassword1"));

        if (!response.IsSuccessStatusCode)
            return;

        HideAddUser();
        await LoadUsers();
    }

    private async Task LoadRolesForUser(string username)
    {
        var response = await Http.GetAsync($"api/auth/users/{username}/roles");
        if (!response.IsSuccessStatusCode)
        {
            _userRoles = Array.Empty<string>();
            return;
        }

        _userRoles = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<string>>() ?? Array.Empty<string>();
    }
}
