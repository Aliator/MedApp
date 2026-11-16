namespace MedApp.Application.Common.Email;

/// <summary>
/// A change to someone's account that they're told about by email, so they notice one they didn't make
/// (<see cref="ISecurityNotices"/>).
/// </summary>
public enum SecurityNotice
{
    /// <summary>Their password was reset with an emailed link.</summary>
    PasswordReset,

    TwoFactorEnabled,
    TwoFactorDisabled,

    /// <summary>An admin turned their two-factor authentication off.</summary>
    TwoFactorReset,

    RecoveryCodesRegenerated,

    /// <summary>A recovery code was used to sign in.</summary>
    RecoveryCodeUsed,

    /// <summary>Their email address changed (sent to the old one).</summary>
    EmailChanged,

    /// <summary>Their email address was removed (sent to the removed one).</summary>
    EmailRemoved,
}
