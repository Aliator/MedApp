using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

public interface IIdentityUserService
{
    Task<IdentityResult> CreateUserAsync(
        string username,
        string password,
        CancellationToken ct);
    
    Task<IdentityResult> DeleteUserAsync(
        string username,
        CancellationToken ct);
}