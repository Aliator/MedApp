using System.Net;
using FluentAssertions;
using MedApp.API.Middleware;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Api;

/// <summary>
/// The browser security headers and Content Security Policies, through the whole pipeline as Production runs it
/// (<see cref="SecurityHeaders"/>): on API answers, errors, refusals and pages.
/// </summary>
[TestFixture]
public sealed class SecurityHeadersTests : IntegrationTest
{
    private static void ShouldHaveBrowserHeaders(HttpResponseMessage response)
    {
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("strict-origin-when-cross-origin");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Permissions-Policy").Single().Should().Contain("camera=()").And.Contain("geolocation=()");
    }

    private static void ShouldHaveTheApiPolicy(HttpResponseMessage response)
    {
        response.Headers.GetValues("Content-Security-Policy").Should().Equal("default-src 'none'; frame-ancestors 'none'");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Test]
    public async Task Api_SignedIn_IsNoStoreWithTheApiPolicy()
    {
        var admin = await SignInAsAdminAsync();

        var response = await (await admin.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        ShouldHaveBrowserHeaders(response);
        ShouldHaveTheApiPolicy(response);
    }

    [TestCase("/api/patients/search", HttpStatusCode.Unauthorized)]
    [TestCase("/api/no-such-endpoint", HttpStatusCode.NotFound)]
    public async Task Api_RefusedOrUnknown_StillGetsTheHeaders(string url, HttpStatusCode status)
    {
        var response = await (await NewClient().GetAsync(url)).ShouldHaveStatusAsync(status);

        ShouldHaveBrowserHeaders(response);
        ShouldHaveTheApiPolicy(response);
    }

    [Test]
    public async Task Api_SignInRefused_StillGetsTheHeaders()
    {
        var response = await NewClient().PostAsync("/api/auth/login",
            System.Net.Http.Json.JsonContent.Create(new { username = AdminUsername, password = WrongPassword }));

        await response.ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
        ShouldHaveTheApiPolicy(response);
    }

    [Test]
    public async Task Page_GetsTheStrictPolicyWithANewNonceEachTime()
    {
        var first = await (await NewClient().GetAsync("/patients")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var second = await (await NewClient().GetAsync("/patients")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        ShouldHaveBrowserHeaders(first);
        var policy = first.Headers.GetValues("Content-Security-Policy").Single();
        policy.Should().MatchRegex("script-src 'self' 'nonce-[A-Za-z0-9+/]{22}=='; ")
            .And.Contain("object-src 'none'")
            .And.Contain("base-uri 'none'")
            .And.Contain("frame-ancestors 'none'")
            .And.Contain("connect-src 'self'");
        second.Headers.GetValues("Content-Security-Policy").Single().Should().NotBe(policy);
        // The client-rendered app (no server rendering here) has no inline script for the policy to refuse.
        (await first.Content.ReadAsStringAsync()).Should().Contain(MedAppApiFactory.StubAppMarker);
    }
}
