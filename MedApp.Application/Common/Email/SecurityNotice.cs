namespace MedApp.Application.Common.Email;

/// <summary>
/// A change to someone's account that they're told about by email, so they notice one they didn't make
/// (<see cref="ISecurityNotices"/>).
/// </summary>
public enum SecurityNotice
{
    /// <summary>Their password was reset with an emailed link.</summary>
    ResetByLink,

    TwoFactorEnabled,
    TwoFactorDisabled,

    /// <summary>An admin turned their two-factor authentication off.</summary>
    TwoFactorReset,

    RecoveryCodesRegenerated,

    /// <summary>A recovery code was used to sign in.</summary>
    RecoveryCodeUsed,

    /// <summary>Their email address changed (sent to the old one).</summary>
    AddressChanged,

    /// <summary>Their email address was removed (sent to the removed one).</summary>
    AddressRemoved,

    /// <summary>Someone asked to change their email address (sent to the confirmed one, before the new one is confirmed).</summary>
    AddressChangeRequested,

    /// <summary>Wrong two-factor codes locked the account: someone may have their password.</summary>
    TwoFactorCodesLockedOut,

    /// <summary>Another admin turned two-factor on (sent to every admin with a confirmed address).</summary>
    AdminTwoFactorEnabled,

    /// <summary>Another admin's two-factor was reset or turned off (sent to every admin with a confirmed address).</summary>
    AdminTwoFactorRemoved,

    /// <summary>Two-factor authentication was turned on with codes sent to this address.</summary>
    EmailCodesEnabled,
}
