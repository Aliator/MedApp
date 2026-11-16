using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Authentication.Requests;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// Sign-in throttling through the real pipeline. Every other test runs with the limits out of reach
/// (<see cref="MedAppApiFactory"/>); these put them back, on an API of their own (so their counts start at nothing).
/// </summary>
[TestFixture]
public sealed class LoginThrottlingTests : IntegrationTest
{
    /// <summary>An API with these limits, and a client for it.</summary>
    private static (WebApplicationFactory<Program> Api, HttpClient Client) ApiWithLimits(
        int perClient, int perUsername, bool behindTheIngress = false)
    {
        var api = Api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("LoginThrottling:PerClientPermitLimit", perClient.ToString());
            builder.UseSetting("LoginThrottling:PerUsernamePermitLimit", perUsername.ToString());
            // As the Helm chart sets it (ASPNETCORE_FORWARDEDHEADERS_ENABLED): the client's address from X-Forwarded-For.
            if (behindTheIngress)
                builder.UseSetting("FORWARDEDHEADERS_ENABLED", "true");
        });
        var client = api.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = MedAppApiFactory.BaseAddress,
            AllowAutoRedirect = false,
        });
        return (api, client);
    }

    private static Task<HttpResponseMessage> SignIn(HttpClient client, string username, string password, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(username, password)),
        };
        if (forwardedFor is not null)
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request);
    }

    [Test]
    public async Task Login_SixthAttemptAtOneUsernameInAMinute_Returns429WithRetryAfter()
    {
        // The real limits.
        var (api, client) = ApiWithLimits(perClient: 30, perUsername: 5);
        await using var _ = api;
        var username = UniqueName("nobody");

        for (var i = 0; i < 5; i++)
            await (await SignIn(client, username, WrongPassword)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);

        var refused = await SignIn(client, username, WrongPassword);

        var problem = await refused.ReadAsAsync<ProblemDetails>(HttpStatusCode.TooManyRequests);
        problem.Title.Should().StartWith("Too many sign-in attempts. Try again in");
        refused.Headers.RetryAfter!.Delta!.Value.TotalSeconds.Should().BeInRange(1, 60);
        // Another name still gets its own allowance.
        await (await SignIn(client, AdminUsername, AdminPassword)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task Login_OverTheClientLimit_Returns429EvenWithTheRightPassword()
    {
        var (api, client) = ApiWithLimits(perClient: 3, perUsername: 100);
        await using var _ = api;

        for (var i = 0; i < 3; i++)
            await (await SignIn(client, UniqueName("nobody"), WrongPassword)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);

        var refused = await SignIn(client, AdminUsername, AdminPassword);

        await refused.ShouldHaveStatusAsync(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter.Should().NotBeNull();
        refused.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Test]
    public async Task Login_BehindTheIngress_CountsEachClientByItsForwardedAddress()
    {
        var (api, client) = ApiWithLimits(perClient: 2, perUsername: 100, behindTheIngress: true);
        await using var _ = api;

        for (var i = 0; i < 2; i++)
            await (await SignIn(client, UniqueName("nobody"), WrongPassword, "203.0.113.10")).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);

        await (await SignIn(client, UniqueName("nobody"), WrongPassword, "203.0.113.10")).ShouldHaveStatusAsync(HttpStatusCode.TooManyRequests);
        // Another client, behind the same ingress, has its own allowance.
        await (await SignIn(client, UniqueName("nobody"), WrongPassword, "203.0.113.20")).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
        // Only the last address counts (the one the ingress writes), so a client can't pick its own by adding one.
        await (await SignIn(client, UniqueName("nobody"), WrongPassword, "198.51.100.1, 203.0.113.10")).ShouldHaveStatusAsync(HttpStatusCode.TooManyRequests);
    }

    [TestCase("\uFEFF")]
    [TestCase("\0")]
    [TestCase("\uFF41")]
    public async Task Login_AdminSpelledWithCharactersNoAccountHas_IsUnknownAndCountsNothing(string extra)
    {
        var (api, client) = ApiWithLimits(perClient: 100, perUsername: 5);
        await using var _ = api;
        // A byte order mark in front, a NUL after, or a full-width letter in front: SQL Server would match each to the
        // admin's row, but sign-in never looks them up.
        var variant = extra == "\0" ? AdminUsername + extra : extra + AdminUsername;

        for (var i = 0; i < 8; i++)
            await (await SignIn(client, variant, AdminPassword)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);

        // None counted against the admin: not towards the lockout, nor the per-username limit.
        await (await SignIn(client, AdminUsername, AdminPassword)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task Login_UsernameLongerThanAnyAccountsName_Returns400()
    {
        var response = await SignIn(NewClient(), new string('a', 257), WrongPassword);

        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainKey("Username");
    }

}
