using System.Security.Cryptography;
using FluentValidation;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;
using ModelContextProtocol;

namespace MedApp.DevMcp;

public static class Guards
{
    public static void RequireConfirm(bool confirm, string action)
    {
        if (!confirm)
            throw new McpException($"{action} is destructive. Call again with confirm: true to go ahead.");
    }

    public static void RequireNotProtected(DevMcpOptions options, string username, string action)
    {
        if (options.IsProtected(username))
            throw new McpException(
                $"'{username}' is a protected account (DevMcp:ProtectedUsers in devmcp.json); medapp-dev won't {action} it.");
    }

    public static void Check(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new McpException($"{action} failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }

    /// <summary>Runs a MediatR call and turns FluentValidation failures into a readable tool error.</summary>
    public static async Task<T> Validated<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ValidationException ex)
        {
            throw new McpException("Validation failed: " +
                string.Join("; ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
        }
    }
}

public static class Passwords
{
    /// <summary>Letters and digits only, with at least one upper, lower and digit (meets the Identity rules in appsettings).</summary>
    public static string Generate(int length = 20)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789";
        const string all = upper + lower + digits;
        var chars = new char[length];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < length; i++) chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    /// <summary>Checks a chosen password against the app's Identity password rules, without creating or changing anything.</summary>
    public static async Task<IdentityResult> Validate(UserManager<ApplicationUser> users, string username, string password)
    {
        var errors = new List<IdentityError>();
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, new ApplicationUser { UserName = username }, password);
            errors.AddRange(result.Errors);
        }
        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]);
    }
}
