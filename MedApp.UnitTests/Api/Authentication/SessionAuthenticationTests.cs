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

    private async Task<AuthenticateResult> Authenticate(string? cookie)
    {
        var context = new DefaultHttpContext();
        if (cookie is not null) context.Request.Headers.Cookie = $"{SessionAuthenticationDefaults.CookieName}={cookie}";
        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());
        var handler = new SessionAuthenticationHandler(options.Object, NullLoggerFactory.Instance, UrlEncoder.Default, _sessions.Object);
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
}

[TestFixture]
public sealed class SessionCookieServiceTests
{
    private static SessionCookieService Service(string environment)
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
    public void AppendSessionCookie_InProduction_SetsASecureHttpOnlyLaxCookieUntilTheSessionEnds()
    {
        var header = SetCookie(r => Service(Environments.Production).AppendSessionCookie(r, new LoginResult(SessionId, ExpiresAtUtc)));

        header.Should().StartWith($"{SessionAuthenticationDefaults.CookieName}={SessionId}");
        header.Should().Contain("expires=").And.Contain("secure").And.Contain("samesite=lax").And.Contain("httponly");
    }

    [Test]
    public void AppendSessionCookie_InDevelopment_IsNotSecure()
    {
        var header = SetCookie(r => Service(Environments.Development).AppendSessionCookie(r, new LoginResult(SessionId, ExpiresAtUtc)));

        header.Should().NotContain("secure");
    }

    [Test]
    public void DeleteSessionCookie_ExpiresIt()
    {
        var header = SetCookie(r => Service(Environments.Production).DeleteSessionCookie(r));

        header.Should().StartWith($"{SessionAuthenticationDefaults.CookieName}=;");
        header.Should().Contain("expires=Thu, 01 Jan 1970");
    }
}

[TestFixture]
public sealed class ClaimsPrincipalExtensionsTests
{
    [Test]
    public void GetUserIdAndToProcedureActor_SignedIn_ReadTheirClaims()
    {
        var user = SignedIn("User");

        user.GetUserId().Should().Be(UserId);
        user.ToProcedureActor().Should().Be(new MedApp.Application.Procedures.Common.ProcedureActor(UserId, Username));
    }

    [Test]
    public void GetUserId_NoIdClaim_Throws()
    {
        var act = () => new ClaimsPrincipal(new ClaimsIdentity()).GetUserId();

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ToProcedureActor_NoName_UsesAnEmptyName()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId.ToString())]));

        user.ToProcedureActor().Username.Should().BeEmpty();
    }
}
