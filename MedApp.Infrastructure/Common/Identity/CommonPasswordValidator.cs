using System.Collections.Frozen;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Refuses the 10,000 most common passwords (the NCSC's list of the passwords most used in breaches; see
/// <c>Passwords/NOTICE.txt</c> for its source and licence), ignoring case, so "Password1" is refused as "password1"
/// is. Identity runs it, with its other password rules, whenever a password is set: creating an account, changing or
/// resetting a password. Signing in doesn't, so existing passwords keep working until they're next changed.
/// </summary>
public sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public const string ErrorCode = "CommonPassword";

    private const string ResourceName = "MedApp.Infrastructure.CommonPasswords.txt";

    private static readonly Lazy<FrozenSet<string>> Passwords = new(Load);

    /// <summary>How many passwords the list holds.</summary>
    public static int Count => Passwords.Value.Count;

    public static IdentityError Error => new()
    {
        Code = ErrorCode,
        Description = "This password is one of the most common, so it's easy to guess. Choose a less common one.",
    };

    /// <summary>Whether <paramref name="password"/> is on the list (ignoring case).</summary>
    public static bool IsCommon(string? password) =>
        !string.IsNullOrEmpty(password) && Passwords.Value.Contains(password);

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password) =>
        Task.FromResult(IsCommon(password) ? IdentityResult.Failed(Error) : IdentityResult.Success);

    private static FrozenSet<string> Load()
    {
        using var stream = typeof(CommonPasswordValidator).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"The common password list ({ResourceName}) isn't embedded.");
        using var reader = new StreamReader(stream);

        var passwords = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
                passwords.Add(line);
        }

        return passwords.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }
}
