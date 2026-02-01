using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Identity;

public sealed class IdentityReadService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
    : IIdentityReadService
{
    public async Task<IReadOnlyList<string>> GetUsernamesAsync(CancellationToken ct)
    {
        return await userManager.Users
            .Select(u => u.UserName!)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(CancellationToken ct)
    {
        return await roleManager.Roles
            .Select(r => r.Name!)
            .ToListAsync(ct);
    }
}