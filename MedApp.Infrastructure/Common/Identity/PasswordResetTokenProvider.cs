using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Identity's data protection token provider for password resets alone, with its own lifetime: 30 minutes
/// (<see cref="PasswordResetTokenProviderOptions"/>), where other tokens (email confirmation) last a day. Its tokens are
/// sealed with the data protection keys in the database, so a link made by one copy of the API opens on another.
/// </summary>
public sealed class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "PasswordReset";
}

public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    public PasswordResetTokenProviderOptions()
    {
        Name = PasswordResetTokenProvider.ProviderName;
        TokenLifespan = Lifetime;
    }
}
