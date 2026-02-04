using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

public interface IIdentityRoleService
{
    Task<IdentityRole<Guid>?> AssignRoleAsync(
        string username,
        string role,
        CancellationToken ct);
    
    Task<IdentityResult> DeleteRoleAsync(
        string roleName,
        CancellationToken ct);
}