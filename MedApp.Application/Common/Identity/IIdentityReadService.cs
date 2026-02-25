using MedApp.Contracts.Authentication.Responses;

namespace MedApp.Application.Common.Identity;

public interface IIdentityReadService
{
    Task<IReadOnlyList<string>> GetUsernamesAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetRoleNamesAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetRolesForUserAsync(
        string username,
        CancellationToken ct);
    Task<UserResponse?> GetUserAsync(
        string username,
        CancellationToken ct);

    Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken ct);
}