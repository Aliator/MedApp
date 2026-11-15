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

    /// <summary>A signed-in user, as the session handler builds them.</summary>
    public static ClaimsPrincipal SignedIn(params string[] roles) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim(ClaimTypes.Name, Username),
                .. roles.Select(role => new Claim(ClaimTypes.Role, role)),
            ],
            "Session"));
}
