using MedApp.Domain.Authentication;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Whether the user must choose their own password before doing anything else: set when an admin's action gives
    /// them a generated one (a new account, or a reset), cleared when they change it. The API refuses everything
    /// else while it's set (<c>PasswordChangeRequired</c>).
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// An email address the user (or an admin, when creating the account) asked for, waiting to be confirmed by the
    /// link sent to it. Until then <see cref="Microsoft.AspNetCore.Identity.IdentityUser{TKey}.Email"/> keeps the
    /// confirmed one (or none), which is the only one MedApp sends to. Confirming moves it there, with
    /// <c>EmailConfirmed</c> set.
    /// </summary>
    public string? PendingEmail { get; set; }

    /// <summary>
    /// How many times wrong two-factor codes have locked the account since its last complete sign-in. Each lockout from
    /// codes lasts twice as long as the one before (<c>CodeLockouts</c>), so codes can't be guessed at a steady rate for
    /// ever. A complete sign-in sets it back to 0.
    /// </summary>
    public int TwoFactorLockouts { get; set; }

    /// <summary>
    /// How the account gives its second factor while <c>TwoFactorEnabled</c> is on: an authenticator app or codes by
    /// email; <see cref="Domain.Authentication.TwoFactorMethod.None"/> while it's off. Read it through
    /// <see cref="EffectiveTwoFactorMethod"/>, which treats an account with two-factor on and no method recorded as using
    /// the app (as every such account did before codes by email).
    /// </summary>
    public TwoFactorMethod TwoFactorMethod { get; set; }

    /// <summary>The method in use: none while two-factor is off, otherwise email only when recorded, else the app.</summary>
    public TwoFactorMethod EffectiveTwoFactorMethod =>
        !TwoFactorEnabled ? TwoFactorMethod.None
        : TwoFactorMethod == TwoFactorMethod.Email ? TwoFactorMethod.Email
        : TwoFactorMethod.Authenticator;
}
