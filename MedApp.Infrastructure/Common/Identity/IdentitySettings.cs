namespace MedApp.Infrastructure.Common.Identity;

public sealed class IdentitySettings
{
    public int PasswordRequiredLength { get; init; }
    public bool RequireDigit { get; init; }
    public bool RequireLowercase { get; init; }
    public bool RequireUppercase { get; init; }
    public bool RequireNonAlphanumeric { get; init; }

    public int MaxFailedAccessAttempts { get; init; }
    public int LockoutMinutes { get; init; }
}