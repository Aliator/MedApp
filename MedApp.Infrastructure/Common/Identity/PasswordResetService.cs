using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Forgotten passwords, reset by a link to the account's confirmed address. The token is Identity's password reset token
/// from <see cref="PasswordResetTokenProvider"/> (30 minutes). It's bound to the account's security stamp, which a reset
/// changes, so it works once.
/// </summary>
public sealed class PasswordResetService(
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> identityOptions)
    : IPasswordResetService
{
    /// <summary>At most this many accounts sharing one confirmed address get a link from one request.</summary>
    public const int MaxTargetsPerAddress = 10;

    public async Task<IReadOnlyList<PasswordResetTarget>> FindTargetsAsync(string usernameOrEmail, CancellationToken ct)
    {
        var users = new Dictionary<Guid, ApplicationUser>();

        // As at sign-in, a name no account could have isn't looked up: SQL Server would match some of them (a byte order
        // mark in front, a NUL after) to a real account's name.
        if (UsernameRules.CouldExist(usernameOrEmail, identityOptions.Value.User.AllowedUserNameCharacters)
            && await userManager.FindByNameAsync(usernameOrEmail) is { } byName)
            users[byName.Id] = byName;

        // Several accounts may share an address (one person with an admin and a staff account): each gets its own link.
        if (CouldBeEmail(usernameOrEmail))
        {
            var normalised = userManager.NormalizeEmail(usernameOrEmail);
            var byEmail = await userManager.Users
                .Where(user => user.NormalizedEmail == normalised && user.EmailConfirmed)
                .OrderBy(user => user.Id)
                .Take(MaxTargetsPerAddress)
                .ToListAsync(ct);
            foreach (var user in byEmail)
                users[user.Id] = user;
        }

        var targets = new List<PasswordResetTarget>(users.Count);
        foreach (var user in users.Values)
        {
            if (!user.EmailConfirmed || string.IsNullOrEmpty(user.Email))
                continue;

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            targets.Add(new PasswordResetTarget(user.Id, user.UserName ?? string.Empty, user.Email, token));
        }

        return targets;
    }

    public async Task<PasswordResetResult> ResetAsync(Guid userId, string token, string newPassword, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return new PasswordResetResult(IdentityResult.Failed(IdentityErrors.InvalidToken));

        // The token first: without a valid one, nothing about the account (not even whether a password is its current
        // one) is checked or told.
        var valid = await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens.PasswordResetTokenProvider,
            UserManager<ApplicationUser>.ResetPasswordTokenPurpose,
            token);
        if (!valid)
            return new PasswordResetResult(IdentityResult.Failed(IdentityErrors.InvalidToken));

        if (await userManager.CheckPasswordAsync(user, newPassword))
            return new PasswordResetResult(IdentityResult.Failed(IdentityErrors.SamePassword), user.UserName);

        // They chose it, so any requirement to change an admin's generated password is met: cleared in the reset's own
        // save, or not at all.
        var mustChangePassword = user.MustChangePassword;
        user.MustChangePassword = false;

        var reset = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!reset.Succeeded)
        {
            user.MustChangePassword = mustChangePassword;
            return new PasswordResetResult(
                reset.Errors.Any(e => e.Code == IdentityErrors.InvalidTokenCode)
                    ? IdentityResult.Failed(IdentityErrors.InvalidToken)
                    : reset,
                user.UserName);
        }

        // Proving the address ends a lockout from wrong passwords: the owner can sign in with the new one at once.
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        return new PasswordResetResult(IdentityResult.Success, user.UserName);
    }

    /// <summary>
    /// Whether <paramref name="value"/> could be an email address to look up: an <c>@</c>, and only printable ASCII, so
    /// look-alike forms (full-width letters, invisible characters) that SQL Server might match to a real address aren't
    /// looked up.
    /// </summary>
    public static bool CouldBeEmail(string value) =>
        value.Length <= UsernameRules.MaxLength
        && value.Contains('@')
        && value.All(c => c is > ' ' and <= '~');
}
