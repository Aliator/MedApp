using System.Globalization;
using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Two-factor authentication on ASP.NET Core Identity's authenticator support: Identity keeps each account's key
/// (<c>AuthenticatorKey</c>), recovery codes (<c>RecoveryCodes</c>) and whether it's on (<c>TwoFactorEnabled</c>).
/// Codes are checked by <see cref="Totp"/> against <see cref="TimeProvider"/>, and the step of the last accepted code is
/// kept as a token of the user's (<see cref="LastTimeStepToken"/>), so each code is accepted once: a code seen over a
/// shoulder or replayed can't be used again. Saving that step is an update of the user, which Identity guards with its
/// concurrency stamp, so of two requests with the same code at the same moment only one gets through.
/// </summary>
public sealed class TwoFactorService(
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider)
    : ITwoFactorService
{
    /// <summary>How many recovery codes an account gets.</summary>
    public const int RecoveryCodeCount = 10;

    /// <summary>The login provider of MedApp's own tokens in Identity's user tokens table.</summary>
    public const string TokenProvider = "[MedApp]";

    /// <summary>The time step of the last authenticator code accepted, as a number.</summary>
    public const string LastTimeStepToken = "AuthenticatorLastTimeStep";

    // Identity's own names for the tokens it keeps (UserStoreBase), to forget the recovery codes when two-factor is
    // turned off. Identity has no public way to remove them.
    private const string IdentityTokenProvider = "[AspNetUserStore]";
    private const string RecoveryCodesToken = "RecoveryCodes";

    public async Task<bool> IsEnabledAsync(Guid userId, CancellationToken ct) =>
        await FindAsync(userId) is { TwoFactorEnabled: true };

    public async Task<TwoFactorState?> GetStateAsync(Guid userId, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null)
            return null;

        var roles = await userManager.GetRolesAsync(user);
        var codesLeft = user.TwoFactorEnabled ? await userManager.CountRecoveryCodesAsync(user) : 0;
        return new TwoFactorState(
            user.UserName ?? string.Empty,
            user.TwoFactorEnabled,
            codesLeft,
            roles.Contains(FirstAdminSetup.AdminRole, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<AuthenticatorKey?> ResetAuthenticatorKeyAsync(Guid userId, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null || user.TwoFactorEnabled)
            return null;

        var reset = await userManager.ResetAuthenticatorKeyAsync(user);
        if (!reset.Succeeded)
            return null;

        await userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, LastTimeStepToken);
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        return key is null ? null : new AuthenticatorKey(user.UserName ?? string.Empty, key);
    }

    public async Task<bool> VerifyAuthenticatorCodeAsync(Guid userId, string code, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null)
            return false;

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
            return false;

        byte[] keyBytes;
        try
        {
            keyBytes = Base32.Decode(key);
        }
        catch (FormatException)
        {
            return false;
        }

        if (Totp.Match(keyBytes, code, timeProvider.GetUtcNow()) is not { } step)
            return false;

        // Each code once: a step at or before the last one accepted is a replay.
        var last = await userManager.GetAuthenticationTokenAsync(user, TokenProvider, LastTimeStepToken);
        if (long.TryParse(last, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastStep) && step <= lastStep)
            return false;

        var saved = await userManager.SetAuthenticationTokenAsync(
            user, TokenProvider, LastTimeStepToken, step.ToString(CultureInfo.InvariantCulture));
        return saved.Succeeded;
    }

    public async Task<RecoveryCodeRedemption> RedeemRecoveryCodeAsync(Guid userId, string code, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null || !user.TwoFactorEnabled)
            return new RecoveryCodeRedemption(false, 0);

        var normalised = NormaliseRecoveryCode(code);
        var redeemed = normalised.Length > 0
                       && (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, normalised)).Succeeded;

        return new RecoveryCodeRedemption(redeemed, await userManager.CountRecoveryCodesAsync(user));
    }

    public async Task<RecoveryCodesResult> EnableAsync(Guid userId, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null)
            return new RecoveryCodesResult(IdentityResult.Failed(IdentityErrors.UserNotFound), []);

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        if (codes is null)
            return new RecoveryCodesResult(IdentityResult.Failed(new IdentityError { Description = "The recovery codes couldn't be saved." }), []);

        var enabled = await userManager.SetTwoFactorEnabledAsync(user, true);
        return new RecoveryCodesResult(enabled, enabled.Succeeded ? codes.ToList() : []);
    }

    public async Task<RecoveryCodesResult> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null)
            return new RecoveryCodesResult(IdentityResult.Failed(IdentityErrors.UserNotFound), []);

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        return codes is null
            ? new RecoveryCodesResult(IdentityResult.Failed(new IdentityError { Description = "The recovery codes couldn't be saved." }), [])
            : new RecoveryCodesResult(IdentityResult.Success, codes.ToList());
    }

    public async Task<IdentityResult> DisableAsync(Guid userId, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null)
            return IdentityResult.Failed(IdentityErrors.UserNotFound);

        var disabled = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disabled.Succeeded)
            return disabled;

        // A new key nobody has, so the old app's codes stop working; and no recovery codes.
        var reset = await userManager.ResetAuthenticatorKeyAsync(user);
        if (!reset.Succeeded)
            return reset;

        var codes = await userManager.RemoveAuthenticationTokenAsync(user, IdentityTokenProvider, RecoveryCodesToken);
        if (!codes.Succeeded)
            return codes;

        return await userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, LastTimeStepToken);
    }

    public async Task<int?> LockoutMinutesLeftAsync(Guid userId, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null || !await userManager.IsLockedOutAsync(user))
            return null;

        return await MinutesLeftAsync(user);
    }

    public async Task<int?> RecordFailedCodeAsync(Guid userId, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is null)
            return null;

        await userManager.AccessFailedAsync(user);
        return await userManager.IsLockedOutAsync(user) ? await MinutesLeftAsync(user) : null;
    }

    public async Task RecordSignInAsync(Guid userId, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (user is not null)
            await userManager.ResetAccessFailedCountAsync(user);
    }

    public async Task<int> CountAdminsWithoutTwoFactorAsync(CancellationToken ct) =>
        (await userManager.GetUsersInRoleAsync(FirstAdminSetup.AdminRole)).Count(user => !user.TwoFactorEnabled);

    /// <summary>A recovery code as Identity stores them: no spaces, upper case (they're letters, digits and a dash).</summary>
    public static string NormaliseRecoveryCode(string code) =>
        code.Replace(" ", string.Empty).Trim().ToUpperInvariant();

    private Task<ApplicationUser?> FindAsync(Guid userId) =>
        userManager.FindByIdAsync(userId.ToString());

    /// <summary>Minutes left on the lockout, rounded up, at least one.</summary>
    private async Task<int> MinutesLeftAsync(ApplicationUser user)
    {
        var end = await userManager.GetLockoutEndDateAsync(user);
        var left = end is { } until ? until - timeProvider.GetUtcNow() : TimeSpan.Zero;
        return Math.Max(1, (int)Math.Ceiling(left.TotalMinutes));
    }
}
