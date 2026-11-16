namespace MedApp.Application.Common.Security;

/// <summary>
/// What a security event records. Today they're written to the application log (<see cref="LoggingSecurityEventLog"/>);
/// each maps to an activity log action when that log is wired in (<see cref="ISecurityEventLog"/>).
/// </summary>
public enum SecurityEventKind
{
    /// <summary>
    /// Someone asked for a password reset link: the account, if one with a confirmed email matched (one event each),
    /// otherwise none. Never what was typed.
    /// </summary>
    PasswordResetRequested,

    /// <summary>A password was reset with an emailed link.</summary>
    PasswordResetCompleted,

    /// <summary>Someone asked to use a new email address (a confirmation link was sent). Never the address.</summary>
    EmailChangeRequested,

    EmailConfirmed,
    EmailRemoved,

    TwoFactorEnabled,
    TwoFactorDisabled,

    /// <summary>An admin turned someone's two-factor authentication off, so they can set it up again.</summary>
    TwoFactorReset,

    RecoveryCodesRegenerated,

    /// <summary>Someone signed in with a recovery code.</summary>
    RecoveryCodeUsed,

    /// <summary>A wrong code at the second step of signing in.</summary>
    TwoFactorCodeRejected,

    /// <summary>An admin changed whether admin accounts must use two-factor authentication.</summary>
    TwoFactorRequirementChanged,
}
