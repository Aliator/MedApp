using MedApp.Application.Authentication.Users.Queries.SearchUsers;
using MedApp.Domain.Authentication;
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

    /// <summary>The first admin a deploy makes (FirstAdminSetup): synthetic.</summary>
    public const string FirstAdminUsername = "first-admin";

    public static readonly DateTime FirstAdminCreatedAtUtc =
        new(2026, 11, 15, 9, 30, 0, DateTimeKind.Utc);

    /// <summary>A user's saved app settings: synthetic, as the app writes them.</summary>
    public const string SettingsJson = "{\"medapp.colorMode\":\"light\",\"medapp.textSize\":\"large\"}";

    public static readonly Guid SettingsUserId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly DateTime SettingsSavedAtUtc =
        new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    public static readonly Guid SessionId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>The user whose sessions a change ends (Usernames[1]).</summary>
    public static readonly Guid SessionUserId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>Usernames[1] as the read service returns them, with these roles.</summary>
    public static global::MedApp.Contracts.Authentication.Responses.UserResponse SessionUser(params string[] roles) =>
        new(SessionUserId, Usernames[1], roles);

    public static readonly DateTime SessionExpiresAtUtc =
        new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>When a synthetic session began: sign-in, with an 8-hour session.</summary>
    public static readonly DateTime SessionStartedAtUtc =
        new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>"Now" in session tests: five minutes before a session started at <see cref="SessionStartedAtUtc"/> ends.</summary>
    public static readonly DateTime SessionNowUtc =
        SessionStartedAtUtc.AddHours(8).AddMinutes(-5);

    /// <summary>A session that began at <see cref="SessionStartedAtUtc"/> and lasts the usual 8 hours.</summary>
    public static UserSession CreateSession() =>
        new(SettingsUserId, SessionStartedAtUtc, SessionStartedAtUtc.Add(UserSession.Lifetime), "192.0.2.20", "SyntheticBrowser/1.0");

    public static ApplicationUser CreateValidUser()
    {
        return new ApplicationUser
        {
            UserName = Usernames[0]
        };
    }

    /// <summary>A search for the first page of users with no filters; override what a test needs.</summary>
    public static SearchUsersQuery CreateSearchUsersQuery() => new(1, 10, null, true, null, null);

    /// <summary>A user with a normalized name, as Identity stores it, for search tests.</summary>
    public static ApplicationUser CreateUser(string username)
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant()
        };
    }

    public static IdentityRole<Guid> CreateValidRole()
    {
        return new IdentityRole<Guid>(Roles[0]);
    }
}