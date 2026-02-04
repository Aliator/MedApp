using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class IdentityUserService(
    UserManager<ApplicationUser> userManager) : IIdentityUserService
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
    
    public async Task<IdentityResult> DeleteUserAsync(
        string username,
        CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UserNotFound",
                Description = "User not found."
            });

        return await userManager.DeleteAsync(user);
    }
}