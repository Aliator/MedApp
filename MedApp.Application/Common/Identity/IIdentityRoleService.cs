using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

public interface IIdentityRoleService
{
    Task<IdentityRole<Guid>?> AssignRoleAsync(
        string username,
        string roleName,
        CancellationToken ct);

    Task<IdentityRole<Guid>?> RevokeRoleAsync(
        string username,
        string roleName,
        CancellationToken ct);

    /// <summary>
    /// Sets the user's roles to exactly these (by name, ignoring case), adding and removing in one step: either
    /// every change is made or none is. Fails with <see cref="IdentityErrors.UserNotFound"/> or
    /// <see cref="IdentityErrors.RoleNotFound"/>.
    /// </summary>
    Task<IdentityResult> SetRolesAsync(
        string username,
        IReadOnlyCollection<string> roleNames,
        CancellationToken ct);
}