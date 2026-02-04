using System.Security.Cryptography;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Auth.Responses;
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

    public async Task<IdentityResult> UpdateUserPasswordAsync(
        string username,
        string oldPassword,
        string newPassword,
        CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
            return IdentityResult.Failed(IdentityErrors.UserNotFound);

        var passwordValid = await userManager.CheckPasswordAsync(user, oldPassword);
        if (!passwordValid)
            return IdentityResult.Failed(IdentityErrors.OldPasswordIncorrect);

        return await userManager.ChangePasswordAsync(
            user,
            oldPassword,
            newPassword);
    }

    public async Task<ResetUserPasswordResponse> ResetUserPasswordAsync(
        string username,
        CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
            return new ResetUserPasswordResponse(
                null,
                [IdentityErrors.UserNotFound]);

        var newPassword = GeneratePassword();

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(
            user,
            resetToken,
            newPassword);

        return result.Succeeded
            ? new ResetUserPasswordResponse(newPassword, Array.Empty<IdentityError>())
            : new ResetUserPasswordResponse(null, result.Errors);
    }

    public async Task<IdentityResult> DeleteUserAsync(
        string username,
        CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
            return IdentityResult.Failed(IdentityErrors.UserNotFound);

        return await userManager.DeleteAsync(user);
    }

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        var buffer = new char[16];
        using var rng = RandomNumberGenerator.Create();

        var bytes = new byte[16];
        rng.GetBytes(bytes);

        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = chars[bytes[i] % chars.Length];

        return new string(buffer);
    }
}
