using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class IdentityOptionsConfiguration(IOptions<IdentitySettings> settings)
    : IConfigureOptions<IdentityOptions>
{
    private readonly IdentitySettings _settings = settings.Value;

    public void Configure(IdentityOptions options)
    {
        options.Password.RequiredLength = _settings.PasswordRequiredLength;
        options.Password.RequireDigit = _settings.RequireDigit;
        options.Password.RequireLowercase = _settings.RequireLowercase;
        options.Password.RequireUppercase = _settings.RequireUppercase;
        options.Password.RequireNonAlphanumeric = _settings.RequireNonAlphanumeric;

        options.Lockout.MaxFailedAccessAttempts = _settings.MaxFailedAccessAttempts;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(_settings.LockoutMinutes);
    }
}