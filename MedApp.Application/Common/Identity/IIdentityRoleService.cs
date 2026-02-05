using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

public interface IIdentityRoleService
{
    Task<IdentityRole<Guid>?> CreateRoleAsync(
        string roleName,
        CancellationToken ct);

    Task<IdentityRole<Guid>?> AssignRoleAsync(
        string username,
        string roleName,
        CancellationToken ct);

    Task<IdentityRole<Guid>?> RevokeRoleAsync(
        string username,
        string roleName,
        CancellationToken ct);

    Task<IdentityResult> DeleteRoleAsync(
        string roleName,
        CancellationToken ct);
}