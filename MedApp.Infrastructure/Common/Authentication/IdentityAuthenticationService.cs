using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.Common.Authentication;

/// <summary>
/// Checks a sign-in, counting wrong passwords towards the lockout set under <c>Identity</c> in
/// <c>appsettings.json</c>: a locked account is refused (even with the right password) until its lockout ends, a
/// wrong password counts one more failure (the one that reaches the limit locks it), and a right one clears the count.
/// A name that couldn't be an account's (<see cref="UsernameRules.CouldExist"/>) is unknown, without being looked up.
/// </summary>
public sealed class IdentityAuthenticationService(
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IOptions<IdentityOptions> identityOptions)
    : IAuthenticationService
{
    public async Task<(Guid, string, IEnumerable<string>)>
        ValidateCredentialsAsync(string username, string password)
    {
        // SQL Server would match some names no account has to one that does (a byte order mark in front, a NUL after,
        // full-width letters), so they never reach it.
        if (!UsernameRules.CouldExist(username, identityOptions.Value.User.AllowedUserNameCharacters))
            throw new InvalidCredentialsException();

        var user = await userManager.FindByNameAsync(username);
        if (user is null)
            throw new InvalidCredentialsException();

        if (await userManager.IsLockedOutAsync(user))
            throw await LockedOut(user);

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
                throw await LockedOut(user);
            throw new InvalidCredentialsException(user.Id);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var roles = await userManager.GetRolesAsync(user);

        return (user.Id, user.UserName!, roles);
    }

    /// <summary>The lockout, with the minutes left on it (rounded up, so never 0).</summary>
    private async Task<AccountLockedOutException> LockedOut(ApplicationUser user)
    {
        var end = await userManager.GetLockoutEndDateAsync(user);
        var left = end is { } until ? until - timeProvider.GetUtcNow() : TimeSpan.Zero;
        return new AccountLockedOutException(Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)), user.Id);
    }
}
