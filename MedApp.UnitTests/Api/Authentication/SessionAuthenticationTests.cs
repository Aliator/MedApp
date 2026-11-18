using MedApp.UnitTests.Common.Constants;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Authentication;

[TestFixture]
public sealed class SessionAuthenticationHandlerTests
{
    private Mock<ISessionService> _sessions = null!;

    [SetUp]
    public void SetUp() => _sessions = new Mock<ISessionService>();

    private Task<AuthenticateResult> Authenticate(string? cookie) =>
        Authenticate(cookie, Environments.Production, SessionAuthenticationDefaults.HostCookieName);

    private async Task<AuthenticateResult> Authenticate(string? cookie, string environment, string cookieName)
    {
        var context = new DefaultHttpContext();
        if (cookie is not null) context.Request.Headers.Cookie = $"{cookieName}={cookie}";
        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());
        var cookies = SessionCookieServiceTests.Service(environment);
        var handler = new SessionAuthenticationHandler(options.Object, NullLoggerFactory.Instance, UrlEncoder.Default, _sessions.Object, cookies);
        await handler.InitializeAsync(new AuthenticationScheme(SessionAuthenticationDefaults.Scheme, null, typeof(SessionAuthenticationHandler)), context);
        return await handler.AuthenticateAsync();
    }

    [Test]
    public async Task Authenticate_NoSessionCookie_ReturnsNoResult()
    {
        var result = await Authenticate(null);

        result.None.Should().BeTrue();
    }

    [Test]
    public async Task Authenticate_OutsideDevelopment_IgnoresTheDevelopmentCookie()
    {
        var result = await Authenticate(SessionId.ToString(), Environments.Production, SessionAuthenticationDefaults.DevelopmentCookieName);

        result.None.Should().BeTrue();
    }

    [Test]
    public async Task Authenticate_InDevelopment_ReadsTheDevelopmentCookie()
    {
        _sessions
            .Setup(s => s.ValidateSessionAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionValidationResult(UserId, Username, ["User"]));

        var result = await Authenticate(SessionId.ToString(), Environments.Development, SessionAuthenticationDefaults.DevelopmentCookieName);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task Authenticate_ValidSession_AddsAClaimForEachPermissionTheRolesGrant()
    {
        _sessions
            .Setup(s => s.ValidateSessionAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionValidationResult(UserId, Username, ["Lab"], Permissions: MedApp.Application.Common.Identity.StandardRoles.Lab.Permissions));

        var result = await Authenticate(SessionId.ToString());

        result.Principal!.FindAll(SessionAuthenticationDefaults.PermissionClaim).Select(c => c.Value)
            .Should().BeEquivalentTo(["UseApp", "ViewPatients", "WorkPathways", "AttachFiles", "RecordSpecimens", "ProcessSpecimens"]);
        result.Principal!.HasPermission(MedApp.Application.Common.Identity.Permission.WorkPathways).Should().BeTrue();
        result.Principal!.HasPermission(MedApp.Application.Common.Identity.Permission.ManageUsers).Should().BeFalse();
    }

    [Test]
    public async Task Authenticate_NoPermissions_AddsNoPermissionClaims()
    {
        _sessions
            .Setup(s => s.ValidateSessionAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionValidationResult(UserId, Username, []));

        var result = await Authenticate(SessionId.ToString());

        result.Principal!.FindAll(SessionAuthenticationDefaults.PermissionClaim).Should().BeEmpty();
    }

    [Test]
    public void GetPermissions_LeavesOutNamesCodeDoesNotKnow()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(SessionAuthenticationDefaults.PermissionClaim, "ViewPatients"),
            new Claim(SessionAuthenticationDefaults.PermissionClaim, "RemovedLongAgo"),
        ], "Session"));

        user.GetPermissions().Should().BeEquivalentTo([MedApp.Application.Common.Identity.Permission.ViewPatients]);
    }

    [Test]
    public async Task Authenticate_UnreadableCookie_Fails()
    {
        var result = await Authenticate("not-a-session-id");

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Be("Invalid session id.");
    }

    [Test]
    public async Task Authenticate_UnknownOrExpiredSession_Fails()
    {
        var result = await Authenticate(SessionId.ToString());

        result.Failure!.Message.Should().Be("Session not found or expired.");
    }

    [Test]
    public async Task Authenticate_ValidSession_SignsInTheUserWithTheirRoles()
    {
        _sessions
            .Setup(s => s.ValidateSessionAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionValidationResult(UserId, Username, ["Admin", "User"]));

        var result = await Authenticate(SessionId.ToString());

        result.Succeeded.Should().BeTrue();
        var user = result.Principal!;
        user.Identity!.AuthenticationType.Should().Be(SessionAuthenticationDefaults.Scheme);
        user.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(UserId.ToString());
        user.Identity.Name.Should().Be(Username);
        user.FindAll(ClaimTypes.Role).Select(c => c.Value).Should().Equal("Admin", "User");
        user.FindFirstValue(SessionAuthenticationDefaults.SessionIdClaim).Should().Be(SessionId.ToString());
        user.GetSessionId().Should().Be(SessionId);
        user.HasClaim(c => c.Type == SessionAuthenticationDefaults.MustChangePasswordClaim).Should().BeFalse();
        user.MustChangePassword().Should().BeFalse();
    }

    [Test]
    public async Task Authenticate_UserMustChangePassword_AddsTheClaim()
    {
        _sessions
            .Setup(s => s.ValidateSessionAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionValidationResult(UserId, Username, ["User"], MustChangePassword: true));

        var result = await Authenticate(SessionId.ToString());

        result.Principal!.FindFirstValue(SessionAuthenticationDefaults.MustChangePasswordClaim).Should().Be("true");
        result.Principal!.MustChangePassword().Should().BeTrue();
    }

    [Test]
    public async Task Authenticate_AdminMustSetUpTwoFactor_AddsTheClaim()
    {
        _sessions
            .Setup(s => s.ValidateSessionAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionValidationResult(UserId, Username, ["Admin"], MustSetUpTwoFactor: true));

        var result = await Authenticate(SessionId.ToString());

        result.Principal!.FindFirstValue(SessionAuthenticationDefaults.MustSetUpTwoFactorClaim).Should().Be("true");
        result.Principal!.MustSetUpTwoFactor().Should().BeTrue();
        result.Principal!.MustChangePassword().Should().BeFalse();
    }
}

[TestFixture]
public sealed class SessionCookieServiceTests
{
    public static SessionCookieService Service(string environment)
    {
        var host = new Mock<IWebHostEnvironment>();
        host.SetupGet(h => h.EnvironmentName).Returns(environment);
        return new SessionCookieService(host.Object);
    }

    private static string SetCookie(Action<HttpResponse> act)
    {
        var context = new DefaultHttpContext();
        act(context.Response);
        return context.Response.Headers.SetCookie.ToString();
    }

    [Test]
    public void AppendSessionCookie_InProduction_SetsASecureHttpOnlyLaxHostCookieUntilTheSessionEnds()
    {
        var header = SetCookie(r => Service(Environments.Production).AppendSessionCookie(r, new LoginResult(SessionId, ExpiresAtUtc)));

        header.Should().StartWith($"__Host-medapp.session={SessionId}");
        header.Should().Contain("expires=").And.Contain("secure").And.Contain("samesite=lax").And.Contain("httponly");
        // What browsers require of a __Host- cookie: Secure (above), Path=/ and no Domain.
        header.Should().Contain("path=/;").And.NotContain("domain=");
    }

    [Test]
    public void AppendSessionCookie_InDevelopment_IsNotSecureAndKeepsItsOldName()
    {
        var header = SetCookie(r => Service(Environments.Development).AppendSessionCookie(r, new LoginResult(SessionId, ExpiresAtUtc)));

        header.Should().StartWith($"medapp.session={SessionId}");
        header.Should().NotContain("secure");
    }

    [TestCase("Production", "__Host-medapp.session")]
    [TestCase("Staging", "__Host-medapp.session")]
    [TestCase("Development", "medapp.session")]
    public void CookieName_DependsOnTheEnvironment(string environment, string expected)
    {
        Service(environment).CookieName.Should().Be(expected);
    }

    [Test]
    public void DeleteSessionCookie_ExpiresIt()
    {
        var header = SetCookie(r => Service(Environments.Production).DeleteSessionCookie(r));

        header.Should().StartWith($"{SessionAuthenticationDefaults.HostCookieName}=;");
        header.Should().Contain("expires=Thu, 01 Jan 1970").And.Contain("path=/;").And.Contain("secure");
    }

    [TestCase(null, false, null)]
    [TestCase("not-a-session-id", true, null)]
    [TestCase("0000000e-0000-0000-0000-000000000002", true, "0000000e-0000-0000-0000-000000000002")]
    public void TryGetSessionCookie_ReadsTheSessionId(string? cookie, bool expectedFound, string? expectedId)
    {
        var context = new DefaultHttpContext();
        if (cookie is not null) context.Request.Headers.Cookie = $"{SessionAuthenticationDefaults.HostCookieName}={cookie}";

        var found = Service(Environments.Production).TryGetSessionCookie(context.Request, out var sessionId);

        found.Should().Be(expectedFound);
        sessionId.Should().Be(expectedId is null ? null : Guid.Parse(expectedId));
    }
}

[TestFixture]
public sealed class ClaimsPrincipalExtensionsTests
{
    [Test]
    public void GetUserIdAndToPathwayActor_SignedIn_ReadTheirClaims()
    {
        var user = SignedIn("User");

        user.GetUserId().Should().Be(UserId);
        user.ToPathwayActor().Should().Be(new MedApp.Application.Pathways.Common.PathwayActor(UserId, Username, TestPermissions.Staff));
    }

    [Test]
    public void GetSessionId_NoSessionClaim_IsNull()
    {
        SignedIn("User").GetSessionId().Should().BeNull();
        SignedInWithSession("User").GetSessionId().Should().Be(SessionId);
    }

    [Test]
    public void GetUserId_NoIdClaim_Throws()
    {
        var act = () => new ClaimsPrincipal(new ClaimsIdentity()).GetUserId();

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ToPathwayActor_NoName_UsesAnEmptyName()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId.ToString())]));

        user.ToPathwayActor().Username.Should().BeEmpty();
    }

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    [TestCase("User", true, "Admin")]
    public void ToCalendarActor_SignedIn_ReadsTheirIdNameAndPermissions(string role, bool isAdmin, params string[] moreRoles)
    {
        var user = SignedIn([role, .. moreRoles]);

        user.ToCalendarActor().Should().Be(new MedApp.Application.Calendar.Common.CalendarActor(UserId, Username, TestPermissions.ForRoles([role, .. moreRoles])));
        user.ToCalendarActor().CanArrangeMeetings.Should().Be(isAdmin);
    }

    [Test]
    public void ToCalendarActor_NoName_UsesAnEmptyName()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId.ToString())]));

        var actor = user.ToCalendarActor();

        actor.Username.Should().BeEmpty();
        actor.Permissions.Should().BeEmpty();
    }

    [Test]
    public void ToCalendarActor_NoIdClaim_Throws()
    {
        var act = () => new ClaimsPrincipal(new ClaimsIdentity()).ToCalendarActor();

        act.Should().Throw<InvalidOperationException>();
    }
}
