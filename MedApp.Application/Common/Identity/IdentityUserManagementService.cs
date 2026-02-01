using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

public sealed class IdentityUserManagementService(
    UserManager<ApplicationUser> userManager)
{
    public async Task<IdentityResult> CreateUserAsync(
        string username,
        string password,
        CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = username,
            CreatedAt = DateTime.UtcNow
        };

        return await userManager.CreateAsync(user, password);
    }
}