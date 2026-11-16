using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Infrastructure.Common.Identity;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>Signing in and out through the API: the session cookie, and when a session stops working.</summary>
[TestFixture]
public sealed class SignInTests : IntegrationTest
{
    private const string PatientsSearch = "/api/patients/search";

    [Test]
    public async Task Login_RightPassword_SetsHttpOnlySecureLaxSessionCookie()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, AdminPassword));

        var login = await response.ReadAsAsync<LoginResponse>();
        login.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddHours(8), TimeSpan.FromMinutes(1));
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(SessionAuthenticationDefaults.HostCookieName + "="));
        cookie.Should().Contain($"{SessionAuthenticationDefaults.HostCookieName}={login.SessionId}");
        cookie.ToLowerInvariant().Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=lax");
        // What browsers require of a __Host- cookie: Secure, Path=/ and no Domain.
        cookie.ToLowerInvariant().Should().Contain("path=/").And.NotContain("domain=");
    }

    [Test]
    public async Task WhoAmI_SignedIn_ReturnsTheUserAndTheirRoles()
    {
        var client = await SignInAsAdminAsync();

        var me = await (await client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();

        me.IsAuthenticated.Should().BeTrue();
        me.Name.Should().Be(AdminUsername);
        me.Roles.Should().BeEquivalentTo([AdminRole, UserRole]);
    }

    [Test]
    public async Task WhoAmI_NoSession_ReturnsNotAuthenticated()
    {
        var me = await (await NewClient().GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();

        me.IsAuthenticated.Should().BeFalse();
        me.Name.Should().BeNull();
        me.Roles.Should().BeEmpty();
    }

    [Test]
    public async Task Logout_SignedIn_RevokesTheSessionEvenIfTheCookieIsKept()
    {
        var client = NewClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, AdminPassword)))
            .ReadAsAsync<LoginResponse>();

        var logout = await client.PostAsync("/api/auth/logout", null);

        await logout.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        logout.Headers.GetValues("Set-Cookie").Should().Contain(c =>
            c.StartsWith(SessionAuthenticationDefaults.HostCookieName + "=;") && c.Contains("expires=Thu, 01 Jan 1970"));
        // A copy of the cookie taken before logging out no longer works.
        var replay = WithSessionCookie(login.SessionId);
        await (await replay.GetAsync(PatientsSearch)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_WrongPassword_Returns401Problem()
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, WrongPassword));

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Unauthorized);
        problem.Status.Should().Be(401);
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Test]
    public async Task Login_UnknownUser_Returns401LikeAWrongPassword()
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(UniqueName("nobody"), WrongPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Request_NoSession_Returns401()
    {
        await (await NewClient().GetAsync(PatientsSearch)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Request_SessionCookieNotAGuid_Returns401()
    {
        var client = NewClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{SessionAuthenticationDefaults.HostCookieName}=not-a-session");

        await (await client.GetAsync(PatientsSearch)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Request_UnknownSession_Returns401()
    {
        await (await WithSessionCookie(Guid.NewGuid()).GetAsync(PatientsSearch)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Request_ExpiredSession_Returns401()
    {
        var client = await SignInAsAdminAsync();
        await (await client.GetAsync(PatientsSearch)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await ExecuteSqlAsync($"UPDATE UserSessions SET ExpiresAtUtc = {DateTime.UtcNow.AddMinutes(-5)}");

        await (await client.GetAsync(PatientsSearch)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Request_SessionRevokedByAnAdmin_Returns401()
    {
        var client = await SignInAsAdminAsync();

        await ExecuteSqlAsync($"UPDATE UserSessions SET RevokedAtUtc = {DateTime.UtcNow}");

        await (await client.GetAsync(PatientsSearch)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_WrongPasswordUpToTheLimit_LocksTheAccountEvenForTheRightPassword()
    {
        var limit = await RunInScopeAsync(services =>
            Task.FromResult(services.GetRequiredService<IOptions<IdentitySettings>>().Value.MaxFailedAccessAttempts));
        limit.Should().BeGreaterThan(1);
        var client = NewClient();

        for (var attempt = 1; attempt < limit; attempt++)
        {
            await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, WrongPassword)))
                .ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
        }

        var locking = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, WrongPassword));
        var rightPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, AdminPassword));

        await locking.ShouldHaveStatusAsync(HttpStatusCode.Locked);
        var problem = await rightPassword.ReadAsAsync<ProblemDetails>(HttpStatusCode.Locked);
        problem.Title.Should().Contain("minute");
    }

    [Test]
    public async Task Login_RightPasswordBeforeTheLimit_ClearsTheFailures()
    {
        var limit = await RunInScopeAsync(services =>
            Task.FromResult(services.GetRequiredService<IOptions<IdentitySettings>>().Value.MaxFailedAccessAttempts));
        var client = NewClient();

        for (var attempt = 1; attempt < limit; attempt++)
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, WrongPassword));
        await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        // The count started again, so one more wrong password doesn't lock the account.
        await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, WrongPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    private static HttpClient WithSessionCookie(Guid sessionId)
    {
        var client = NewClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{SessionAuthenticationDefaults.HostCookieName}={sessionId}");
        return client;
    }
}
