namespace MedApp.Domain.Authentication;

/// <summary>
/// How an account gives its second factor when it signs in. One method per account: switching means turning one off and
/// the other on (except from email codes to the app, which can be done in one step). Stored as a number, so values never
/// change.
/// </summary>
public enum TwoFactorMethod
{
    /// <summary>Two-factor authentication is off: the password alone signs in.</summary>
    None = 0,

    /// <summary>A code from an authenticator app (TOTP): the stronger choice, and the one admins can be required to use.</summary>
    Authenticator = 1,

    /// <summary>
    /// A short-lived code MedApp emails to the account's confirmed address. Weaker than the app: anyone who gets into the
    /// mailbox gets the code (NIST SP 800-63B doesn't accept email for out-of-band authentication).
    /// </summary>
    Email = 2,
}
