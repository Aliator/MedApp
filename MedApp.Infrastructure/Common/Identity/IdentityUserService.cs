using System.Security.Cryptography;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class IdentityUserService(
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> identityOptions) : IIdentityUserService
{
    private readonly IdentityOptions _identityOptions = identityOptions.Value;
    
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

    private string GeneratePassword()
    {
        var rules = _identityOptions.Password;

        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string nonAlpha = "!@#$%";

        var required = new List<char>(4);
        var allowed = "";

        if (rules.RequireUppercase)
        {
            required.Add(upper[RandomNumberGenerator.GetInt32(upper.Length)]);
            allowed += upper;
        }

        if (rules.RequireLowercase)
        {
            required.Add(lower[RandomNumberGenerator.GetInt32(lower.Length)]);
            allowed += lower;
        }

        if (rules.RequireDigit)
        {
            required.Add(digits[RandomNumberGenerator.GetInt32(digits.Length)]);
            allowed += digits;
        }

        if (rules.RequireNonAlphanumeric)
        {
            required.Add(nonAlpha[RandomNumberGenerator.GetInt32(nonAlpha.Length)]);
            allowed += nonAlpha;
        }

        if (allowed.Length == 0)
            allowed = upper + lower + digits;

        var length = rules.RequiredLength < 1 ? 8 : rules.RequiredLength;
        if (length < required.Count)
            length = required.Count;

        var uniqueRequired = rules.RequiredUniqueChars < 1 ? 1 : rules.RequiredUniqueChars;
        if (length < uniqueRequired)
            length = uniqueRequired;

        var buffer = new char[length];
        var used = new HashSet<char>();

        for (var i = 0; i < required.Count; i++)
        {
            buffer[i] = required[i];
            used.Add(required[i]);
        }

        for (var i = required.Count; i < buffer.Length; i++)
        {
            if (used.Count < uniqueRequired)
            {
                char c;
                do
                {
                    c = allowed[RandomNumberGenerator.GetInt32(allowed.Length)];
                } while (!used.Add(c));

                buffer[i] = c;
            }
            else
            {
                buffer[i] = allowed[RandomNumberGenerator.GetInt32(allowed.Length)];
            }
        }

        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        return new string(buffer);
    }
}
