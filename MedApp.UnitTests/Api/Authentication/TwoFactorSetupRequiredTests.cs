using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using Microsoft.AspNetCore.Http;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Authentication;

/// <summary>
/// The hold on an admin who must set up two-factor authentication, and the requests every hold allows (which the
/// forced password change now shares).
/// </summary>
[TestFixture]
public sealed class TwoFactorSetupRequiredTests
{
    private static ClaimsPrincipal MustSetUp(bool alsoMustChangePassword = false)
    {
        var user = alsoMustChangePassword ? MustChangePassword("Admin") : SignedIn("Admin");
        ((ClaimsIdentity)user.Identity!).AddClaim(new Claim(SessionAuthenticationDefaults.MustSetUpTwoFactorClaim, "true"));
        return user;
    }

    private static async Task<(HttpContext Context, bool Passed, JsonElement Body)> Run(AccountHold hold, HttpContext context)
    {
        await hold.InvokeAsync(context);
        context.Response.Body.Position = 0;
        var body = context.Response.Body.Length > 0 ? await JsonDocument.ParseAsync(context.Response.Body) : null;
        return (context, Passed, body?.RootElement ?? default);
    }

    private static bool Passed;

    private static Task<(HttpContext Context, bool Passed, JsonElement Body)> RunTwoFactor(ClaimsPrincipal user, string method, string path)
    {
        Passed = false;
        var context = new DefaultHttpContext { User = user, Response = { Body = new MemoryStream() } };
        context.Request.Method = method;
        context.Request.Path = path;
        return Run(new TwoFactorSetupRequired(_ => { Passed = true; return Task.CompletedTask; }), context);
    }

    private static Task<(HttpContext Context, bool Passed, JsonElement Body)> RunPassword(ClaimsPrincipal user, string method, string path)
    {
        Passed = false;
        var context = new DefaultHttpContext { User = user, Response = { Body = new MemoryStream() } };
        context.Request.Method = method;
        context.Request.Path = path;
        return Run(new PasswordChangeRequired(_ => { Passed = true; return Task.CompletedTask; }), context);
    }

    [TestCase("GET", "/api/auth/whoami")]
    [TestCase("GET", "/api/auth/me/settings")]
    [TestCase("GET", "/api/auth/features")]
    [TestCase("GET", "/api/auth/me/two-factor")]
    [TestCase("POST", "/api/auth/me/two-factor/authenticator")]
    [TestCase("POST", "/API/Auth/Me/Two-Factor/Enable")]
    [TestCase("POST", "/api/auth/me/two-factor/email/code")]
    [TestCase("POST", "/api/auth/login")]
    [TestCase("POST", "/api/auth/login/two-factor")]
    [TestCase("POST", "/api/auth/login/two-factor/resend")]
    [TestCase("POST", "/api/auth/logout")]
    [TestCase("POST", "/api/auth/password/forgot")]
    [TestCase("POST", "/api/auth/password/reset")]
    [TestCase("POST", "/api/auth/email/confirm")]
    [TestCase("GET", "/patients")]
    public async Task MustSetUp_AllowsSettingItUpAndWhatEveryHoldAllows(string method, string path)
    {
        var (_, passed, _) = await RunTwoFactor(MustSetUp(), method, path);

        passed.Should().BeTrue();
    }

    [TestCase("GET", "/api/patients/search")]
    [TestCase("GET", "/api/notifications/hub")]
    [TestCase("POST", "/api/auth/me/two-factor/disable")]
    [TestCase("POST", "/api/auth/me/two-factor/recovery-codes")]
    [TestCase("POST", "/api/auth/me/two-factor/email")]
    [TestCase("POST", "/api/auth/me/two-factor/email/enable")]
    [TestCase("GET", "/api/auth/me/email")]
    [TestCase("GET", "/api/auth/security-settings")]
    [TestCase("PUT", "/api/auth/security-settings")]
    [TestCase("POST", "/api/auth/me/two-factor")]
    [TestCase("GET", "/api/auth/me/two-factor/enable")]
    [TestCase("GET", "/api/auth/session")]
    public async Task MustSetUp_RefusesEverythingElseWithItsCode(string method, string path)
    {
        var (context, passed, body) = await RunTwoFactor(MustSetUp(), method, path);

        passed.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        body.GetProperty("title").GetString().Should().Be(TwoFactorSetupRequired.Title);
        body.GetProperty("code").GetString().Should().Be(TwoFactorSetupRequired.Code);
    }

    [Test]
    public async Task MustAlsoChangePassword_LeavesItToThePasswordHold()
    {
        var (_, passed, _) = await RunTwoFactor(MustSetUp(alsoMustChangePassword: true), "GET", "/api/patients/search");

        passed.Should().BeTrue();
    }

    [Test]
    public async Task NotHeld_PassesEverything()
    {
        var (_, passed, _) = await RunTwoFactor(SignedIn("Admin"), "GET", "/api/patients/search");

        passed.Should().BeTrue();
    }

    [TestCase("GET", "/api/auth/features")]
    [TestCase("POST", "/api/auth/login/two-factor")]
    [TestCase("POST", "/api/auth/password/forgot")]
    [TestCase("POST", "/api/auth/password/reset")]
    [TestCase("POST", "/api/auth/email/confirm")]
    public async Task MustChangePassword_AlsoAllowsTheEndpointsUsedBeforeSigningIn(string method, string path)
    {
        var (_, passed, _) = await RunPassword(MustChangePassword("User"), method, path);

        passed.Should().BeTrue();
    }

    [TestCase("POST", "/api/auth/me/two-factor/authenticator")]
    [TestCase("GET", "/api/auth/me/two-factor")]
    public async Task MustChangePassword_RefusesTwoFactorSetUpUntilThePasswordIsChanged(string method, string path)
    {
        var (_, passed, body) = await RunPassword(MustChangePassword("Admin"), method, path);

        passed.Should().BeFalse();
        body.GetProperty("code").GetString().Should().Be(PasswordChangeRequired.Code);
    }
}
