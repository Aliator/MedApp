using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class IdentityUserManagementService(
    UserManager<ApplicationUser> userManager)
    : IUserManagementService
{
    public async Task<Guid> CreateUserAsync(
        string username,
        string password,
        CancellationToken ct)
    {
        var existing = await userManager.FindByNameAsync(username);
        if (existing is not null)
            throw new IdentityOperationException("User already exists.");

        var user = new ApplicationUser
        {
            UserName = username,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.Description).ToArray());

            throw new IdentityOperationException(errors);
        }

        return user.Id;
    }
}