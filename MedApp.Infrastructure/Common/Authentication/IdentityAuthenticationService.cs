using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Authentication;

/// <summary>
/// Checks a sign-in, counting wrong passwords towards the lockout set under <c>Identity</c> in
/// <c>appsettings.json</c>: a locked account is refused (even with the right password) until its lockout ends, a
/// wrong password counts one more failure (the one that reaches the limit locks it), and a right one clears the count.
/// </summary>
public sealed class IdentityAuthenticationService(
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider)
    : IAuthenticationService
{
    public async Task<(Guid, string, IEnumerable<string>)>
        ValidateCredentialsAsync(string username, string password)
    {
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
