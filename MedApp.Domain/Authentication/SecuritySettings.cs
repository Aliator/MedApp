namespace MedApp.Domain.Authentication;

/// <summary>
/// The deployment's sign-in rules that admins set (Admin settings): one row, <see cref="SingletonId"/>. With no row,
/// every rule is off, as a new database starts.
/// </summary>
public sealed class SecuritySettings
{
    /// <summary>The ID of the one row.</summary>
    public const int SingletonId = 1;

    public int Id { get; private set; } = SingletonId;

    /// <summary>
    /// Whether accounts with the Admin role must use two-factor authentication. One without it is held, after signing in,
    /// until they set it up (<c>TwoFactorSetupRequired</c>), and can't turn it off.
    /// </summary>
    public bool RequireTwoFactorForAdmins { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>The admin who last changed them.</summary>
    public Guid? UpdatedByUserId { get; set; }
}
