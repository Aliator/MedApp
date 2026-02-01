namespace MedApp.Application.Common.Identity;

public interface IUserRoleService
{
    Task AssignRoleAsync(
        string username,
        string role,
        CancellationToken ct);
}