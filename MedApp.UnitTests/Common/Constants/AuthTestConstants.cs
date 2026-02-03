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

    public const string Password = "password";
    public const string Token = "jwt-token";

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