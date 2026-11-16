using MedApp.Contracts.Authentication.Responses;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

public interface IIdentityUserService
{
    /// <summary>
    /// Creates an account with a generated password that meets the Identity password rules (the same generator as
    /// <see cref="ResetUserPasswordAsync"/>). The result carries the password, once, when the account was created.
    /// The account must change it at first sign-in (its must-change-password flag is set).
    /// </summary>
    Task<CreateUserResult> CreateUserAsync(
        string username,
        CancellationToken ct);

    /// <summary>
    /// Changes a user's own password, given the current one. A successful change clears their must-change-password
    /// flag, in the same update.
    /// </summary>
    Task<IdentityResult> UpdateUserPasswordAsync(
        string username,
        string oldPassword,
        string newPassword,
        CancellationToken ct);

    /// <summary>
    /// Gives a user a new generated password, to be passed on to them, and sets their must-change-password flag in the
    /// same update, so they must replace it with their own.
    /// </summary>
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