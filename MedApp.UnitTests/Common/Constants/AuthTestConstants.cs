using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.UnitTests.Common.Constants;

public static class AuthTestConstants
{
    public static readonly IReadOnlyList<string> Usernames =
    [
        "username",
        "username2"
    ];

    public static readonly IReadOnlyList<string> Roles =
    [
        "Admin",
        "User"
    ];

    public static readonly IReadOnlyList<string> Passwords =
    [
        "oldPassword",
        "newPassword",
    ];

    public const string ValidPassword = "ValidPassword1";

    public static readonly Guid SessionId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly DateTime SessionExpiresAtUtc =
        new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static ApplicationUser CreateValidUser()
    {
        return new ApplicationUser
        {
            UserName = Usernames[0]
        };
    }

    public static IdentityRole<Guid> CreateValidRole()
    {
        return new IdentityRole<Guid>(Roles[0]);
    }
}