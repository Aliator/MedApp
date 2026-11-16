using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using Microsoft.AspNetCore.Http;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Authentication;

[TestFixture]
public sealed class PasswordChangeRequiredTests
{
    private static async Task<(HttpContext Context, bool Passed, JsonElement Body)> Run(
        ClaimsPrincipal user,
        string method,
        string path,
        string? routeUsername = null)
    {
        var context = new DefaultHttpContext { User = user, Response = { Body = new MemoryStream() } };
        context.Request.Method = method;
        context.Request.Path = path;
        // Routing has matched the endpoint by the time the middleware runs, so the route's values are set.
        if (routeUsername is not null)
            context.Request.RouteValues["username"] = routeUsername;

        var passed = false;
        var middleware = new PasswordChangeRequired(_ =>
        {
            passed = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = context.Response.Body.Length > 0 ? await JsonDocument.ParseAsync(context.Response.Body) : null;
        return (context, passed, body?.RootElement ?? default);
    }

    private static readonly string OwnPasswordPath = $"/api/auth/users/{Username}/update-password";

    [TestCase("GET", "/api/patients/search")]
    [TestCase("GET", "/api/auth/me/settings")]
    [TestCase("GET", "/api/notifications/hub")]
    [TestCase("PUT", "/api/auth/users/test.staff/reset-password")]
    public async Task InvokeAsync_UserWhoNeedNotChangeTheirPassword_PassesEverything(string method, string path)
    {
        var (_, passed, _) = await Run(SignedIn("User"), method, path);

        passed.Should().BeTrue();
    }

    [Test]
    public async Task InvokeAsync_SignedOut_PassesForAuthorizationToDecide()
    {
        var (_, passed, _) = await Run(new ClaimsPrincipal(new ClaimsIdentity()), "GET", "/api/patients/search");

        passed.Should().BeTrue();
    }

    [Test]
    public async Task InvokeAsync_ClaimWithoutASignedInIdentity_Passes()
    {
        // An unauthenticated identity can't carry a session's claims; the check is on the signed-in user only.
        var identity = new ClaimsIdentity([new Claim(SessionAuthenticationDefaults.MustChangePasswordClaim, "true")]);

        var (_, passed, _) = await Run(new ClaimsPrincipal(identity), "GET", "/api/patients/search");

        passed.Should().BeTrue();
    }

    [TestCase("GET", "/api/auth/whoami")]
    [TestCase("GET", "/API/Auth/WhoAmI")]
    [TestCase("POST", "/api/auth/login")]
    [TestCase("POST", "/api/auth/logout")]
    [TestCase("GET", "/api/auth/me/settings")]
    public async Task InvokeAsync_MustChangePassword_AllowsTheAllowlist(string method, string path)
    {
        var (context, passed, _) = await Run(MustChangePassword("User"), method, path);

        passed.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Test]
    public async Task InvokeAsync_MustChangePassword_AllowsChangingTheirOwnPassword()
    {
        var (_, passed, _) = await Run(MustChangePassword("User"), "PUT", OwnPasswordPath, Username);

        passed.Should().BeTrue();
    }

    [TestCase("/")]
    [TestCase("/patients")]
    [TestCase("/change-password")]
    [TestCase("/assets/app.js")]
    [TestCase("/apiary")]
    public async Task InvokeAsync_MustChangePassword_PassesPagesAndFilesForTheWebAppToRedirect(string path)
    {
        var (_, passed, _) = await Run(MustChangePassword("User"), "GET", path);

        passed.Should().BeTrue();
    }

    [TestCase("GET", "/api/patients/search", null)]
    [TestCase("GET", "/api/notifications", null)]
    [TestCase("POST", "/api/notifications/hub/negotiate", null)]
    [TestCase("GET", "/api/notifications/hub", null)]
    [TestCase("PUT", "/api/auth/me/settings", null)]
    [TestCase("POST", "/api/auth/whoami", null)]
    [TestCase("GET", "/api/auth/logout", null)]
    [TestCase("GET", "/api/auth/login", null)]
    [TestCase("GET", "/api", null)]
    [TestCase("GET", "/api/no-such-endpoint", null)]
    // Someone else's password, by route value or by a name that differs only in case.
    [TestCase("PUT", "/api/auth/users/test.staff/update-password", "test.staff")]
    [TestCase("PUT", "/api/auth/users/TEST.ADMIN/update-password", "TEST.ADMIN")]
    // Their own name, but not the update-password endpoint.
    [TestCase("PUT", "/api/auth/users/test.admin/reset-password", "test.admin")]
    [TestCase("PUT", "/api/auth/users/test.admin/roles", "test.admin")]
    [TestCase("DELETE", "/api/auth/users/test.admin", "test.admin")]
    [TestCase("POST", "/api/auth/users/test.admin/update-password", "test.admin")]
    [TestCase("PUT", "/api/auth/users/test.admin/update-password/extra", "test.admin")]
    [TestCase("PUT", "/api/other/users/test.admin/update-password", "test.admin")]
    [TestCase("PUT", "/api/auth/people/test.admin/update-password", "test.admin")]
    [TestCase("PUT", "/api/auth/users/test.admin/update-password", null)]
    public async Task InvokeAsync_MustChangePassword_RefusesEveryOtherApiRequest(string method, string path, string? routeUsername)
    {
        var (context, passed, body) = await Run(MustChangePassword("Admin", "User"), method, path, routeUsername);

        passed.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.ContentType.Should().Be("application/problem+json");
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status403Forbidden);
        body.GetProperty("title").GetString().Should().Be("Password change required");
        body.GetProperty("code").GetString().Should().Be("password_change_required");
    }
}
