namespace MedApp.Application.Common.Identity;

public interface IUserManagementService
{
    Task<Guid> CreateUserAsync(
        string username,
        string password,
        CancellationToken ct);
}