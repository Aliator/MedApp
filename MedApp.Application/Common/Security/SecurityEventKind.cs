namespace MedApp.Application.Common.Security;

/// <summary>
/// What a security event records: each is an activity log action (<see cref="ActivitySecurityEventLog.ActionFor"/>).
/// Sign-ins themselves, the second step's included, are recorded by the sign-in handlers.
/// </summary>
public enum SecurityEventKind
{
    /// <summary>
    /// Someone asked for a password reset link: the account, if one with a confirmed email matched (one event each),
    /// otherwise none. Never what was typed.
    /// </summary>
    ResetLinkRequested,

    /// <summary>A password was reset with an emailed link.</summary>
    ResetByLink,

    /// <summary>Someone asked to use a new email address (a confirmation link was sent). Never the address.</summary>
    AddressChangeRequested,

    AddressConfirmed,
    AddressRemoved,

    TwoFactorEnabled,
    TwoFactorDisabled,

    /// <summary>An admin turned someone's two-factor authentication off, so they can set it up again.</summary>
    TwoFactorReset,

    RecoveryCodesRegenerated,

    /// <summary>Someone signed in with a recovery code.</summary>
    RecoveryCodeUsed,

    /// <summary>An admin changed whether admin accounts must use two-factor authentication.</summary>
    TwoFactorRequirementChanged,

    /// <summary>Two-factor authentication was turned on with codes by email.</summary>
    EmailCodesEnabled,

    /// <summary>Two-factor authentication with codes by email was turned off.</summary>
    EmailCodesDisabled,
}
