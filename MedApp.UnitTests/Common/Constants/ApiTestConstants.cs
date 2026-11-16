using System.Security.Claims;

namespace MedApp.UnitTests.Common.Constants;

public static class ApiTestConstants
{
    public static readonly Guid UserId = Guid.Parse("0000000e-0000-0000-0000-000000000001");
    public static readonly Guid SessionId = Guid.Parse("0000000e-0000-0000-0000-000000000002");
    public static readonly Guid RecordId = Guid.Parse("0000000e-0000-0000-0000-000000000003");
    public static readonly Guid OtherId = Guid.Parse("0000000e-0000-0000-0000-000000000004");
    public static readonly DateTime ExpiresAtUtc = new(2026, 6, 15, 20, 0, 0, DateTimeKind.Utc);
    public const string Username = "test.admin";
    public const string OtherUsername = "test.staff";
    public const string Password = "Synthetic-Password-1";
    public const string IpAddress = "192.0.2.10";
    public const string UserAgent = "SyntheticBrowser/1.0";
    public const string Email = "test.admin@example.test";
    public const string TwoFactorChallenge = "synthetic-challenge-token";
    public const string AuthenticatorCode = "123456";

    /// <summary>A signed-in user, as the session handler builds them.</summary>
    public static ClaimsPrincipal SignedIn(params string[] roles) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim(ClaimTypes.Name, Username),
                .. roles.Select(role => new Claim(ClaimTypes.Role, role)),
            ],
            "Session"));

    /// <summary>A signed-in user, with the ID of the session they came with.</summary>
    public static ClaimsPrincipal SignedInWithSession(params string[] roles)
    {
        var user = SignedIn(roles);
        ((ClaimsIdentity)user.Identity!).AddClaim(
            new Claim(MedApp.API.Common.Authentication.SessionAuthenticationDefaults.SessionIdClaim, SessionId.ToString()));
        return user;
    }

    /// <summary>A signed-in user who must change the password an admin gave them before doing anything else.</summary>
    public static ClaimsPrincipal MustChangePassword(params string[] roles)
    {
        var user = SignedIn(roles);
        ((ClaimsIdentity)user.Identity!).AddClaim(
            new Claim(MedApp.API.Common.Authentication.SessionAuthenticationDefaults.MustChangePasswordClaim, "true"));
        return user;
    }
}
