using MedApp.Contracts.Authentication.Responses;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

public interface IIdentityUserService
{
    Task<IdentityResult> CreateUserAsync(
        string username,
        string password,
        CancellationToken ct);
    
    Task<IdentityResult> UpdateUserPasswordAsync(
        string username,
        string oldPassword,
        string newPassword,
        CancellationToken ct);
    
    Task<ResetUserPasswordResponse> ResetUserPasswordAsync(
        string username,
        CancellationToken ct);
    
    /// <summary>
    /// Checks a user's own password, e.g. to confirm a delete. A wrong one counts towards lockout, as a failed
    /// sign-in would; a right one clears the count. Fails with <see cref="IdentityErrors.PasswordIncorrect"/>,
    /// <see cref="IdentityErrors.LockedOut"/> or <see cref="IdentityErrors.UserNotFound"/>.
    /// </summary>
    Task<IdentityResult> VerifyPasswordAsync(
        string username,
        string password,
        CancellationToken ct);

    Task<IdentityResult> DeleteUserAsync(
        string username,
        CancellationToken ct);
}